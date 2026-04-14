namespace BlindMatchPAS.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int UserCount { get; set; }

        public int StudentCount { get; set; }

        public int SupervisorCount { get; set; }

        public int ProjectGroupCount { get; set; }

        public int ProposalCount { get; set; }

        public int PendingProposalCount { get; set; }

        public int UnderReviewProposalCount { get; set; }

        public int MatchedProposalCount { get; set; }

        public int ResearchAreaCount { get; set; }

        public bool IsSubmissionWindowOpen { get; set; }

        public bool IsMatchingWindowOpen { get; set; }

        public List<AllocationSummaryViewModel> RecentAllocations { get; set; } = new();

        public List<AuditLogSummaryViewModel> RecentAuditLogs { get; set; } = new();

        public List<NotificationEmailSummaryViewModel> RecentEmails { get; set; } = new();
    }
}
