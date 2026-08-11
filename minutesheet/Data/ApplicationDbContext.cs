using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace minutesheet.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Department> Departments => Set<Department>();
        public DbSet<MinuteSheet> MinuteSheets => Set<MinuteSheet>();
        public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
        public DbSet<SheetComment> Comments => Set<SheetComment>();
        public DbSet<SheetShare> SheetShares => Set<SheetShare>();
        public DbSet<SheetSuggestion> SheetSuggestions => Set<SheetSuggestion>();
        public DbSet<DomainVocabularyTerm> DomainVocabularyTerms => Set<DomainVocabularyTerm>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<MinuteSheet>()
                .HasOne(m => m.CreatedByUser)
                .WithMany()
                .HasForeignKey(m => m.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApprovalStep>()
                .HasOne(a => a.MinuteSheet)
                .WithMany(m => m.ApprovalSteps)
                .HasForeignKey(a => a.MinuteSheetId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MinuteSheet>()
                .HasIndex(m => m.Token)
                .IsUnique();

            builder.Entity<SheetComment>()
                .HasOne(c => c.MinuteSheet)
                .WithMany(m => m.Comments)
                .HasForeignKey(c => c.MinuteSheetId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SheetShare>()
                .HasOne(s => s.MinuteSheet)
                .WithMany()
                .HasForeignKey(s => s.MinuteSheetId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SheetShare>()
                .HasOne(s => s.SharedByUser)
                .WithMany()
                .HasForeignKey(s => s.SharedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SheetSuggestion>()
                .HasOne(s => s.MinuteSheet)
                .WithMany()
                .HasForeignKey(s => s.MinuteSheetId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SheetSuggestion>()
                .HasOne(s => s.AuthorUser)
                .WithMany()
                .HasForeignKey(s => s.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Department>().HasData(
                new Department { Id = 1, Name = "HR", EmployeeCount = 0 },
                new Department { Id = 2, Name = "ICT", EmployeeCount = 0 },
                new Department { Id = 3, Name = "Finance", EmployeeCount = 0 },
                new Department { Id = 4, Name = "Admin", EmployeeCount = 0 });

            builder.Entity<DomainVocabularyTerm>().HasData(
                new DomainVocabularyTerm { Id = 1, Category = VocabularyCategory.Person, Term = "Ahmad", Aliases = "Ahmed, Ahmet", IsActive = true },
                new DomainVocabularyTerm { Id = 2, Category = VocabularyCategory.Person, Term = "Hammad", Aliases = "Hamad", IsActive = true },
                new DomainVocabularyTerm { Id = 3, Category = VocabularyCategory.Person, Term = "Umair", Aliases = "Omair, Umer", IsActive = true },
                new DomainVocabularyTerm { Id = 4, Category = VocabularyCategory.Person, Term = "Waqas", Aliases = "Wakas, Wakkas", IsActive = true },
                new DomainVocabularyTerm { Id = 5, Category = VocabularyCategory.Product, Term = "Minute Sheet", Aliases = "MinuteSheet, Minutesheet", IsActive = true },
                new DomainVocabularyTerm { Id = 6, Category = VocabularyCategory.Platform, Term = "GitHub", Aliases = "Git hub, Github", IsActive = true },
                new DomainVocabularyTerm { Id = 7, Category = VocabularyCategory.Platform, Term = "Jira", Aliases = "Jeera, Geera", IsActive = true },
                new DomainVocabularyTerm { Id = 8, Category = VocabularyCategory.Technology, Term = "Blazor", Aliases = "Blazer, Blazar", IsActive = true },
                new DomainVocabularyTerm { Id = 9, Category = VocabularyCategory.Technology, Term = ".NET", Aliases = "Dot net, dotnet", IsActive = true },
                new DomainVocabularyTerm { Id = 10, Category = VocabularyCategory.Abbreviation, Term = "QA", Aliases = "Q A, cue a", IsActive = true },
                new DomainVocabularyTerm { Id = 11, Category = VocabularyCategory.Abbreviation, Term = "UAT", Aliases = "U A T, you a t", IsActive = true });
        }
    }
}
