using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace minutesheet.Data.Seed
{
    // Restores the snapshot in Data/Seed/seed-data.json into an empty database so a
    // fresh clone comes up with the same departments, users, sheets and workflow
    // history the app was developed against.
    //
    // Idempotent: every row is keyed by its original Id (users by Id/email) and is
    // skipped when already present, so running this repeatedly is a no-op.
    //
    // Seeded accounts all share SeedPassword — the export deliberately carries no
    // password hashes, so real credentials never land in source control.
    public static class DatabaseSnapshotSeeder
    {
        public const string SeedPassword = "Abcd1234!";

        private const string SnapshotFileName = "seed-data.json";

        public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
        {
            await using var scope = services.CreateAsyncScope();
            var sp = scope.ServiceProvider;

            var config = sp.GetRequiredService<IConfiguration>();
            var env = sp.GetRequiredService<IHostEnvironment>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseSnapshotSeeder));

            // Off outside Development unless explicitly switched on — restoring a
            // dev snapshot over a real database would be destructive.
            var enabled = config.GetValue<bool?>("Seed:LoadSnapshot") ?? env.IsDevelopment();
            if (!enabled)
            {
                return;
            }

            var path = Path.Combine(AppContext.BaseDirectory, "Data", "Seed", SnapshotFileName);
            var snapshot = await SeedSnapshot.LoadAsync(path, ct);
            if (snapshot is null)
            {
                logger.LogInformation("No seed snapshot found at {Path}; skipping snapshot seeding.", path);
                return;
            }

            var ctx = sp.GetRequiredService<ApplicationDbContext>();
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

            await SeedRolesAsync(roleManager, snapshot);
            await SeedDepartmentsAsync(ctx, snapshot, ct);
            await SeedUsersAsync(userManager, snapshot, logger);
            await SeedSheetsAsync(ctx, snapshot, ct);
            await SeedChildRowsAsync(ctx, snapshot, ct);
            await SeedVocabularyAsync(ctx, snapshot, ct);

            logger.LogInformation("Seed snapshot applied from {Path}.", path);
        }

        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, SeedSnapshot snapshot)
        {
            var roles = Roles.All.Concat(snapshot.UserRoles.Select(r => r.RoleName)).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }

        private static async Task SeedDepartmentsAsync(ApplicationDbContext ctx, SeedSnapshot snapshot, CancellationToken ct)
        {
            var existing = await ctx.Departments.Select(d => d.Id).ToListAsync(ct);
            var missing = snapshot.Departments.Where(d => !existing.Contains(d.Id)).ToList();
            if (missing.Count == 0)
            {
                return;
            }

            ctx.Departments.AddRange(missing.Select(d => new Department
            {
                Id = d.Id,
                Name = d.Name,
                EmployeeCount = d.EmployeeCount
            }));

            await SaveWithIdentityInsertAsync(ctx, "Departments", ct);
        }

        private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager, SeedSnapshot snapshot, ILogger logger)
        {
            foreach (var u in snapshot.Users)
            {
                var user = await userManager.FindByEmailAsync(u.Email) ?? await userManager.FindByIdAsync(u.Id);
                if (user is null)
                {
                    user = new ApplicationUser
                    {
                        Id = u.Id,
                        UserName = u.UserName,
                        Email = u.Email,
                        EmailConfirmed = u.EmailConfirmed,
                        PhoneNumber = u.PhoneNumber,
                        FullName = u.FullName,
                        EmployeeNo = u.EmployeeNo,
                        Designation = (Designation)u.Designation,
                        DepartmentId = u.DepartmentId,
                        AvatarPath = u.AvatarPath
                    };

                    var result = await userManager.CreateAsync(user, SeedPassword);
                    if (!result.Succeeded)
                    {
                        logger.LogWarning("Could not seed user {Email}: {Errors}", u.Email,
                            string.Join("; ", result.Errors.Select(e => e.Description)));
                        continue;
                    }
                }

                foreach (var role in snapshot.UserRoles.Where(r => r.UserEmail.Equals(u.Email, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!await userManager.IsInRoleAsync(user, role.RoleName))
                    {
                        await userManager.AddToRoleAsync(user, role.RoleName);
                    }
                }
            }
        }

        private static async Task SeedSheetsAsync(ApplicationDbContext ctx, SeedSnapshot snapshot, CancellationToken ct)
        {
            var existing = await ctx.MinuteSheets.Select(s => s.Id).ToListAsync(ct);
            // A sheet's creator must exist, otherwise the FK insert fails.
            var userIds = await ctx.Users.Select(u => u.Id).ToListAsync(ct);

            var missing = snapshot.Sheets
                .Where(s => !existing.Contains(s.Id) && userIds.Contains(s.CreatedByUserId))
                .ToList();
            if (missing.Count == 0)
            {
                return;
            }

            ctx.MinuteSheets.AddRange(missing.Select(s => new MinuteSheet
            {
                Id = s.Id,
                Title = s.Title,
                Category = (SheetCategory)s.Category,
                Amount = s.Amount,
                Currency = s.Currency,
                DescriptionHtml = s.DescriptionHtml,
                Summary = s.Summary,
                ActionItems = s.ActionItems,
                NextMeetingAgenda = s.NextMeetingAgenda,
                AttachmentFileName = s.AttachmentFileName,
                AttachmentStoredPath = s.AttachmentStoredPath,
                IsConfidential = s.IsConfidential,
                CreatedByUserId = s.CreatedByUserId,
                CreatedAt = s.CreatedAt,
                Status = s.Status,
                Token = s.Token,
                DepartmentId = s.DepartmentId,
                IntendedForDepartmentId = s.IntendedForDepartmentId
            }));

            await SaveWithIdentityInsertAsync(ctx, "MinuteSheets", ct);
        }

        private static async Task SeedChildRowsAsync(ApplicationDbContext ctx, SeedSnapshot snapshot, CancellationToken ct)
        {
            var sheetIds = await ctx.MinuteSheets.Select(s => s.Id).ToListAsync(ct);
            var userIds = await ctx.Users.Select(u => u.Id).ToListAsync(ct);

            var existingSteps = await ctx.ApprovalSteps.Select(s => s.Id).ToListAsync(ct);
            var steps = snapshot.ApprovalSteps
                .Where(s => !existingSteps.Contains(s.Id) && sheetIds.Contains(s.MinuteSheetId))
                .Select(s => new ApprovalStep
                {
                    Id = s.Id,
                    MinuteSheetId = s.MinuteSheetId,
                    StepIndex = s.StepIndex,
                    Action = (ApprovalAction)s.Action,
                    Status = (ApprovalStepStatus)s.Status,
                    ActedAt = s.ActedAt,
                    ApproverEmail = s.ApproverEmail
                })
                .ToList();
            if (steps.Count > 0)
            {
                ctx.ApprovalSteps.AddRange(steps);
                await SaveWithIdentityInsertAsync(ctx, "ApprovalSteps", ct);
            }

            var stepIds = await ctx.ApprovalSteps.Select(s => s.Id).ToListAsync(ct);
            var existingComments = await ctx.Comments.Select(c => c.Id).ToListAsync(ct);
            var comments = snapshot.Comments
                .Where(c => !existingComments.Contains(c.Id) && sheetIds.Contains(c.MinuteSheetId) && userIds.Contains(c.AuthorUserId))
                .Select(c => new SheetComment
                {
                    Id = c.Id,
                    MinuteSheetId = c.MinuteSheetId,
                    // Drop a dangling step reference rather than failing the whole insert.
                    ApprovalStepId = c.ApprovalStepId is int sid && stepIds.Contains(sid) ? sid : null,
                    AuthorUserId = c.AuthorUserId,
                    AuthorName = c.AuthorName,
                    Kind = (CommentKind)c.Kind,
                    Body = c.Body,
                    CreatedAt = c.CreatedAt
                })
                .ToList();
            if (comments.Count > 0)
            {
                ctx.Comments.AddRange(comments);
                await SaveWithIdentityInsertAsync(ctx, "Comments", ct);
            }

            var existingShares = await ctx.SheetShares.Select(s => s.Id).ToListAsync(ct);
            var shares = snapshot.Shares
                .Where(s => !existingShares.Contains(s.Id) && sheetIds.Contains(s.MinuteSheetId) && userIds.Contains(s.SharedByUserId))
                .Select(s => new SheetShare
                {
                    Id = s.Id,
                    MinuteSheetId = s.MinuteSheetId,
                    SharedByUserId = s.SharedByUserId,
                    SharedWithEmail = s.SharedWithEmail,
                    SharedAt = s.SharedAt
                })
                .ToList();
            if (shares.Count > 0)
            {
                ctx.SheetShares.AddRange(shares);
                await SaveWithIdentityInsertAsync(ctx, "SheetShares", ct);
            }

            var existingSuggestions = await ctx.SheetSuggestions.Select(s => s.Id).ToListAsync(ct);
            var suggestions = snapshot.Suggestions
                .Where(s => !existingSuggestions.Contains(s.Id) && sheetIds.Contains(s.MinuteSheetId) && userIds.Contains(s.AuthorUserId))
                .Select(s => new SheetSuggestion
                {
                    Id = s.Id,
                    MinuteSheetId = s.MinuteSheetId,
                    AuthorUserId = s.AuthorUserId,
                    AuthorName = s.AuthorName,
                    IsAllOk = s.IsAllOk,
                    Body = s.Body,
                    CreatedAt = s.CreatedAt
                })
                .ToList();
            if (suggestions.Count > 0)
            {
                ctx.SheetSuggestions.AddRange(suggestions);
                await SaveWithIdentityInsertAsync(ctx, "SheetSuggestions", ct);
            }
        }

        private static async Task SeedVocabularyAsync(ApplicationDbContext ctx, SeedSnapshot snapshot, CancellationToken ct)
        {
            var existing = await ctx.DomainVocabularyTerms.Select(t => t.Id).ToListAsync(ct);
            var missing = snapshot.Vocabulary.Where(t => !existing.Contains(t.Id)).ToList();
            if (missing.Count == 0)
            {
                return;
            }

            ctx.DomainVocabularyTerms.AddRange(missing.Select(t => new DomainVocabularyTerm
            {
                Id = t.Id,
                Category = (VocabularyCategory)t.Category,
                Term = t.Term,
                Aliases = t.Aliases,
                IsActive = t.IsActive
            }));

            await SaveWithIdentityInsertAsync(ctx, "DomainVocabularyTerms", ct);
        }

        // The snapshot keeps original primary keys so foreign keys stay valid, which
        // means SQL Server needs IDENTITY_INSERT toggled around the write. The toggle
        // is connection-scoped, so the connection is held open across the save.
        private static async Task SaveWithIdentityInsertAsync(ApplicationDbContext ctx, string table, CancellationToken ct)
        {
            var connection = ctx.Database.GetDbConnection();
            var alreadyOpen = connection.State == System.Data.ConnectionState.Open;
            if (!alreadyOpen)
            {
                await ctx.Database.OpenConnectionAsync(ct);
            }

            try
            {
                await ctx.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT dbo.[{table}] ON", ct);
                await ctx.SaveChangesAsync(ct);
            }
            finally
            {
                await ctx.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT dbo.[{table}] OFF", ct);
                if (!alreadyOpen)
                {
                    await ctx.Database.CloseConnectionAsync();
                }
            }
        }
    }
}
