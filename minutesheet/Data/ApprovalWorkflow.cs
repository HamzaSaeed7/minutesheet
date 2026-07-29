namespace minutesheet.Data
{
    // Single source of truth for the approval rule: a sheet is Approved only when
    // every step has been Approved (Review is an intermediate action).
    public static class ApprovalWorkflow
    {
        public static int ApprovedCount(IEnumerable<ApprovalStep> steps) =>
            steps.Count(s => s.Status == ApprovalStepStatus.Approved);

        public static bool IsFullyApproved(ICollection<ApprovalStep> steps) =>
            steps.Count > 0 && steps.All(s => s.Status == ApprovalStepStatus.Approved);

        public static string StatusFor(MinuteSheet sheet) =>
            IsFullyApproved(sheet.ApprovalSteps) ? "Approved" : "Pending";
    }
}
