using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using minutesheet.Components.Account.Pages;
using minutesheet.Components.Account.Pages.Manage;
using minutesheet.Data;
using System.Security.Claims;
using System.Text.Json;

namespace Microsoft.AspNetCore.Routing
{
    internal static class IdentityComponentsEndpointRouteBuilderExtensions
    {
        // These endpoints are required by the Identity Razor components defined in the /Components/Account/Pages directory of this project.
        public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var accountGroup = endpoints.MapGroup("/Account");

            accountGroup.MapPost("/PerformExternalLogin", (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromForm] string provider,
                [FromForm] string returnUrl) =>
            {
                IEnumerable<KeyValuePair<string, StringValues>> query = [
                    new("ReturnUrl", returnUrl),
                    new("Action", ExternalLogin.LoginCallbackAction)];

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/ExternalLogin",
                    QueryString.Create(query));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
                return TypedResults.Challenge(properties, [provider]);
            });

            accountGroup.MapPost("/Logout", async (
                ClaimsPrincipal user,
                SignInManager<ApplicationUser> signInManager,
                [FromForm] string returnUrl) =>
            {
                await signInManager.SignOutAsync();
                return TypedResults.LocalRedirect($"~/{returnUrl}");
            });

            // Account deletion runs here (not in the interactive Settings circuit) because
            // SignOutAsync must write the auth cookie to the HTTP response — impossible over
            // a Blazor Server WebSocket, which throws "Headers are read-only".
            accountGroup.MapPost("/DeleteAccount", async (
                ClaimsPrincipal claims,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromServices] ApplicationDbContext db,
                [FromServices] IWebHostEnvironment env,
                [FromServices] ILoggerFactory loggerFactory,
                [FromForm] string? password) =>
            {
                var logger = loggerFactory.CreateLogger("Account.DeleteAccount");

                var user = await userManager.GetUserAsync(claims);
                if (user is null)
                {
                    return Results.LocalRedirect("~/Account/Login");
                }

                // Require the current password only when the account has one set.
                if (await userManager.HasPasswordAsync(user) &&
                    !await userManager.CheckPasswordAsync(user, password ?? string.Empty))
                {
                    return Results.LocalRedirect("~/settings?deleteError=password");
                }

                var departmentId = user.DepartmentId;
                var avatarPath = user.AvatarPath;
                var userId = await userManager.GetUserIdAsync(user);

                var result = await userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    return Results.LocalRedirect("~/settings?deleteError=1");
                }

                // Best-effort avatar cleanup — a leftover file isn't worth failing on.
                if (!string.IsNullOrEmpty(avatarPath))
                {
                    try
                    {
                        var full = Path.Combine(env.WebRootPath, avatarPath.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(full))
                        {
                            File.Delete(full);
                        }
                    }
                    catch (IOException) { }
                }

                // Keep the department headcount in sync.
                if (departmentId is int id)
                {
                    var dept = await db.Departments.FirstOrDefaultAsync(d => d.Id == id);
                    if (dept is not null)
                    {
                        dept.EmployeeCount = Math.Max(0, dept.EmployeeCount - 1);
                        await db.SaveChangesAsync();
                    }
                }

                await signInManager.SignOutAsync();
                logger.LogInformation("User with ID '{UserId}' deleted their account.", userId);
                return Results.LocalRedirect("~/Account/Login");
            });

            var manageGroup = accountGroup.MapGroup("/Manage").RequireAuthorization();

            manageGroup.MapPost("/LinkExternalLogin", async (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromForm] string provider) =>
            {
                // Clear the existing external cookie to ensure a clean login process
                await context.SignOutAsync(IdentityConstants.ExternalScheme);

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/Manage/ExternalLogins",
                    QueryString.Create("Action", ExternalLogins.LinkLoginCallbackAction));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, signInManager.UserManager.GetUserId(context.User));
                return TypedResults.Challenge(properties, [provider]);
            });

            var loggerFactory = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var downloadLogger = loggerFactory.CreateLogger("DownloadPersonalData");

            manageGroup.MapPost("/DownloadPersonalData", async (
                HttpContext context,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] AuthenticationStateProvider authenticationStateProvider) =>
            {
                var user = await userManager.GetUserAsync(context.User);
                if (user is null)
                {
                    return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
                }

                var userId = await userManager.GetUserIdAsync(user);
                downloadLogger.LogInformation("User with ID '{UserId}' asked for their personal data.", userId);

                // Only include personal data for download
                var personalData = new Dictionary<string, string>();
                var personalDataProps = typeof(ApplicationUser).GetProperties().Where(
                    prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
                foreach (var p in personalDataProps)
                {
                    personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
                }

                var logins = await userManager.GetLoginsAsync(user);
                foreach (var l in logins)
                {
                    personalData.Add($"{l.LoginProvider} external login provider key", l.ProviderKey);
                }

                personalData.Add("Authenticator Key", (await userManager.GetAuthenticatorKeyAsync(user))!);
                var fileBytes = JsonSerializer.SerializeToUtf8Bytes(personalData);

                context.Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.json");
                return TypedResults.File(fileBytes, contentType: "application/json", fileDownloadName: "PersonalData.json");
            });

            return accountGroup;
        }
    }
}
