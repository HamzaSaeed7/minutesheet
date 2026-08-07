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

        // Approvals are hierarchical: they run in step order. Only the lowest-numbered
        // step that isn't approved yet is open for action; every later step waits for
        // it. Returns null once the whole sheet is approved.
        public static ApprovalStep? CurrentStep(IEnumerable<ApprovalStep> steps) =>
            steps.Where(s => s.Status != ApprovalStepStatus.Approved)
                 .OrderBy(s => s.StepIndex)
                 .FirstOrDefault();

        // True when this step is the one currently open for action.
        public static bool IsActionable(ApprovalStep step, IEnumerable<ApprovalStep> steps)
        {
            var current = CurrentStep(steps);
            return current is not null && current.StepIndex == step.StepIndex;
        }

        public static string StatusFor(MinuteSheet sheet) =>
            IsFullyApproved(sheet.ApprovalSteps) ? "Approved"
            : sheet.ApprovalSteps.Any(s => s.Status == ApprovalStepStatus.Reviewed) ? "Review"
            : "Pending";
    }
}
