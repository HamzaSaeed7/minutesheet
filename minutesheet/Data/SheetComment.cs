using System.ComponentModel.DataAnnotations;

namespace minutesheet.Data
{
    // A single entry in a minute sheet's comment thread: a reviewer's feedback,
    // an approval note, or the creator's resolution of a review.
    public class SheetComment
    {
        public int Id { get; set; }

        public int MinuteSheetId { get; set; }

        public MinuteSheet? MinuteSheet { get; set; }

        // The approval step this comment relates to (null for a general comment).
        public int? ApprovalStepId { get; set; }

        [Required]
        public string AuthorUserId { get; set; } = "";

        // Denormalized display name (full name or email) captured at write time.
        [MaxLength(256)]
        public string AuthorName { get; set; } = "";

        public CommentKind Kind { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Body { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
