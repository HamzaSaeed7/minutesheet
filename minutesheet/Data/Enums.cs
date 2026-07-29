using System.ComponentModel.DataAnnotations;

namespace minutesheet.Data
{
    public enum Designation
    {
        [Display(Name = "Executive")]
        Executive = 1,

        [Display(Name = "Senior Executive")]
        SeniorExecutive = 2,

        [Display(Name = "AM (Assistant Manager)")]
        AssistantManager = 3,

        [Display(Name = "Deputy Manager")]
        DeputyManager = 4,

        [Display(Name = "Section Manager")]
        SectionManager = 5,

        [Display(Name = "Unit Manager")]
        UnitManager = 6
    }

    public enum SheetCategory
    {
        [Display(Name = "Financial")]
        Financial = 1,

        [Display(Name = "Non-Financial")]
        NonFinancial = 2
    }

    public enum ApprovalAction
    {
        [Display(Name = "Review")]
        Review = 1,

        [Display(Name = "Approve")]
        Approve = 2
    }

    // The actual outcome recorded for an approval step (distinct from the
    // designated Action above, which is only the expected/hint action).
    public enum ApprovalStepStatus
    {
        [Display(Name = "Pending")]
        Pending = 1,

        [Display(Name = "Reviewed")]
        Reviewed = 2,

        [Display(Name = "Approved")]
        Approved = 3
    }

    public enum CommentKind
    {
        [Display(Name = "Review")]
        Review = 1,

        [Display(Name = "Approval")]
        Approval = 2,

        [Display(Name = "Resolution")]
        Resolution = 3
    }
}
