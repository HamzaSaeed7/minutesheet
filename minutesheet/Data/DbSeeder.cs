using Microsoft.AspNetCore.Identity;

namespace minutesheet.Data
{
    // Runs once at startup: ensures the Admin/Employee roles exist and that the
    // bootstrap admin account is present. Safe to run repeatedly (idempotent).
    public static class DbSeeder
    {
        private const string AdminEmail = "admin@ffc.com";
        private const string AdminPassword = "Abcd1234!";

        public static async Task SeedAsync(IServiceProvider services)
        {
            await using var scope = services.CreateAsyncScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Ensure both roles exist.
            foreach (var role in Roles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Ensure the bootstrap admin account exists and is in the Admin role.
            var admin = await userManager.FindByEmailAsync(AdminEmail);
            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    UserName = AdminEmail,
                    Email = AdminEmail,
                    EmailConfirmed = true,
                    FullName = "Administrator",
                    EmployeeNo = "ADMIN",
                    Designation = Designation.UnitManager
                };
                await userManager.CreateAsync(admin, AdminPassword);
            }

            if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
            {
                await userManager.AddToRoleAsync(admin, Roles.Admin);
            }

            // 3. Backfill: any existing account with no role becomes an Employee,
            // so accounts created before roles existed are handled consistently.
            foreach (var user in userManager.Users.ToList())
            {
                if ((await userManager.GetRolesAsync(user)).Count == 0)
                {
                    await userManager.AddToRoleAsync(user, Roles.Employee);
                }
            }
        }
    }
}
