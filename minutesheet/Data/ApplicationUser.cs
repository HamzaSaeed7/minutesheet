using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace minutesheet.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        [MaxLength(150)]
        public string FullName { get; set; } = "";

        [MaxLength(50)]
        public string EmployeeNo { get; set; } = "";

        public Designation Designation { get; set; }

        public int? DepartmentId { get; set; }

        public Department? Department { get; set; }

        // Web-root-relative path to the user's uploaded profile picture, e.g. "uploads/avatars/xxxx.png".
        [MaxLength(260)]
        public string? AvatarPath { get; set; }
    }
}
