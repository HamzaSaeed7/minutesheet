using System.ComponentModel.DataAnnotations;

namespace minutesheet.Data
{
    public class Department
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = "";

        // Running count of employees who have signed up under this department.
        public int EmployeeCount { get; set; }

        public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    }
}
