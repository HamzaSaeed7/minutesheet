using System.ComponentModel.DataAnnotations;

namespace minutesheet.Data
{
    public class ApprovalStep
    {
        public int Id { get; set; }

        public int MinuteSheetId { get; set; }

        public MinuteSheet? MinuteSheet { get; set; }

        // 1-based position of this row in the approval table.
        public int StepIndex { get; set; }

        // The designated/expected action for this row (a hint set at creation).
        public ApprovalAction Action { get; set; }

        // The actual outcome recorded by the approver.
        public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Pending;

        // When the approver last acted (reviewed/approved); null while pending.
        public DateTime? ActedAt { get; set; }

        [Required]
        [MaxLength(256)]
        public string ApproverEmail { get; set; } = "";
    }
}
