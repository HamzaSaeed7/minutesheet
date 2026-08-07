using System.ComponentModel.DataAnnotations;

namespace minutesheet.Data
{
    // Feedback left by a user a sheet was shared with: either a suggestion for
    // what to add or change, or an "It's all OK" acknowledgement.
    public class SheetSuggestion
    {
        public int Id { get; set; }

        public int MinuteSheetId { get; set; }

        public MinuteSheet? MinuteSheet { get; set; }

        [Required]
        public string AuthorUserId { get; set; } = "";

        public ApplicationUser? AuthorUser { get; set; }

        // Denormalized display name captured at write time.
        [MaxLength(256)]
        public string AuthorName { get; set; } = "";

        // When true this is an "It's all OK" acknowledgement instead of text.
        public bool IsAllOk { get; set; }

        [MaxLength(2000)]
        public string Body { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}