using System.ComponentModel.DataAnnotations;

namespace minutesheet.Data
{
    public class MinuteSheet
    {
        public int Id { get; set; }

        public SheetCategory Category { get; set; }

        // Rich-text HTML produced by the Quill editor.
        public string DescriptionHtml { get; set; } = "";
        
        public string? Summary { get; set; }

        // Original file name as uploaded by the user (null when no attachment).
        [MaxLength(260)]
        public string? AttachmentFileName { get; set; }

        // Relative path under wwwroot where the file is stored (e.g. uploads/{guid}.pdf).
        [MaxLength(400)]
        public string? AttachmentStoredPath { get; set; }

        [Required]
        public string CreatedByUserId { get; set; } = "";

        public ApplicationUser? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string Status { get; set; } = "Submitted";

        // Unguessable token used to build the sheet's unique shareable link.
        public Guid Token { get; set; } = Guid.NewGuid();

        public ICollection<ApprovalStep> ApprovalSteps { get; set; } = new List<ApprovalStep>();

        public ICollection<SheetComment> Comments { get; set; } = new List<SheetComment>();
    }
}
