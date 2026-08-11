using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using minutesheet.Components;
using minutesheet.Components.Account;
using minutesheet.Data;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.Configure<EmailSettings>(options =>
{
    builder.Configuration.GetSection("EmailSettings").Bind(options);

    var envPassword = Environment.GetEnvironmentVariable("GOOGLE_APP_PASSWORD")
        ?? Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD")
        ?? Environment.GetEnvironmentVariable("EMAIL_PASSWORD")
        ?? Environment.GetEnvironmentVariable("EmailSettings__Password")
        ?? Environment.GetEnvironmentVariable("EmailSettings:Password");

    if (!string.IsNullOrWhiteSpace(envPassword))
    {
        options.Password = envPassword;
    }

    var envUser = Environment.GetEnvironmentVariable("GOOGLE_APP_USER")
        ?? Environment.GetEnvironmentVariable("GMAIL_APP_USER")
        ?? Environment.GetEnvironmentVariable("EMAIL_USER")
        ?? Environment.GetEnvironmentVariable("EmailSettings__User")
        ?? Environment.GetEnvironmentVariable("EmailSettings:User");

    if (!string.IsNullOrWhiteSpace(envUser))
    {
        options.User = envUser;
        if (string.IsNullOrWhiteSpace(options.From))
        {
            options.From = envUser;
        }
    }

    var envHost = Environment.GetEnvironmentVariable("EMAIL_HOST")
        ?? Environment.GetEnvironmentVariable("EmailSettings__Host");

    if (!string.IsNullOrWhiteSpace(envHost))
    {
        options.Host = envHost;
    }
});
builder.Services.AddHttpClient();
builder.Services.AddSingleton<SmtpEmailSender>();
builder.Services.AddSingleton<IEmailSender<ApplicationUser>>(sp => sp.GetRequiredService<SmtpEmailSender>());

// Notification mail is delivered off the request path so workflow actions stay snappy.
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailBackgroundService>();

builder.Services.AddHttpClient<minutesheet.Services.OpenRouter.IOpenRouterClient, minutesheet.Services.OpenRouter.OpenRouterClient>()
    .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

builder.Services.AddHttpClient<minutesheet.Services.IGroqTranscriptionService, minutesheet.Services.GroqTranscriptionService>()
    .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

builder.Services.AddScoped<minutesheet.Services.DocumentSummarizationService>();
builder.Services.AddScoped<minutesheet.Services.ILocalWhisperTranscriptionService, minutesheet.Services.LocalWhisperTranscriptionService>();
builder.Services.AddSingleton<minutesheet.Services.SheetPdfService>();
builder.Services.AddScoped<minutesheet.Services.ToastService>();

builder.Services.AddScoped<minutesheet.Services.ITranscriptCorrectionService, minutesheet.Services.TranscriptCorrectionService>();
builder.Services.AddScoped<minutesheet.Services.ITranslationService, minutesheet.Services.TranslationService>();
builder.Services.AddScoped<minutesheet.Services.IDictationPipelineService, minutesheet.Services.DictationPipelineService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Seed roles (Admin/Employee) and the bootstrap admin account on startup.
await DbSeeder.SeedAsync(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapPost("/api/transcriptions", async (
    IFormFile audio,
    [FromForm] string language,
    minutesheet.Services.IDictationPipelineService dictationPipelineService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var text = await dictationPipelineService.ProcessAudioAsync(audio, language, cancellationToken);
        return Results.Ok(new { text });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (HttpRequestException exception)
    {
        return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
    }
}).RequireAuthorization().DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
