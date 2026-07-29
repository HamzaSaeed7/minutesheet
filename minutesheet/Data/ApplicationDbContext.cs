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

            builder.Entity<Department>().HasData(
                new Department { Id = 1, Name = "HR", EmployeeCount = 0 },
                new Department { Id = 2, Name = "ICT", EmployeeCount = 0 },
                new Department { Id = 3, Name = "Finance", EmployeeCount = 0 },
                new Department { Id = 4, Name = "Admin", EmployeeCount = 0 });
        }
    }
}
