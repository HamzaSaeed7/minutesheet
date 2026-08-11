using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace minutesheet.Data
{
    public class MinuteSheet
    {
        public int Id { get; set; }

        // Short human-readable title; a sheet is referenced by this across the app.
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = "";

        public SheetCategory Category { get; set; }

        // Monetary amount — only applicable when Category == Financial.
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Amount { get; set; }

        // Currency code for the amount (e.g. PKR, USD, EUR).
        [MaxLength(10)]
        public string? Currency { get; set; }

        // Rich-text HTML produced by the Quill editor.
        public string DescriptionHtml { get; set; } = "";

        public string? Summary { get; set; }

        // Extracted action items stored as JSON string.
        public string? ActionItems { get; set; }

        // Generated next meeting agenda stored as JSON string.
        public string? NextMeetingAgenda { get; set; }

        // Original file name as uploaded by the user (null when no attachment).
        [MaxLength(260)]
        public string? AttachmentFileName { get; set; }

        // Relative path under wwwroot where the file is stored (e.g. uploads/{guid}.pdf).
        [MaxLength(400)]
        public string? AttachmentStoredPath { get; set; }

        // When true, only the creator, listed approvers and admins can view the sheet.
        public bool IsConfidential { get; set; }

        [Required]
        public string CreatedByUserId { get; set; } = "";

        public ApplicationUser? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string Status { get; set; } = "Submitted";

        // Unguessable token used to build the sheet's unique shareable link.
        public Guid Token { get; set; } = Guid.NewGuid();

        // Department that initiated this minute sheet.
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        // Department this minute sheet is intended for (its recipient).
        public int? IntendedForDepartmentId { get; set; }
        public Department? IntendedForDepartment { get; set; }

        public ICollection<ApprovalStep> ApprovalSteps { get; set; } = new List<ApprovalStep>();

        public ICollection<SheetComment> Comments { get; set; } = new List<SheetComment>();
    }
}
