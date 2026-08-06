using System.ComponentModel.DataAnnotations;

namespace minutesheet.Data
{
    // Grants a user (identified by email) read-only access to a minute sheet
    // they are not otherwise part of. Sharing only ever grants view access —
    // the recipient cannot edit, approve or delete the sheet.
    public class SheetShare
    {
        public int Id { get; set; }

        public int MinuteSheetId { get; set; }

        public MinuteSheet? MinuteSheet { get; set; }

        // The user who shared the sheet.
        [Required]
        public string SharedByUserId { get; set; } = "";

        public ApplicationUser? SharedByUser { get; set; }

        // The recipient's email. Lookups are case-insensitive.
        [Required]
        [MaxLength(256)]
        public string SharedWithEmail { get; set; } = "";

        public DateTime SharedAt { get; set; } = DateTime.UtcNow;
    }
}
