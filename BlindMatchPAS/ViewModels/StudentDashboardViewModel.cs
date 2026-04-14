namespace BlindMatchPAS.ViewModels
{
    public class StudentDashboardViewModel
    {
        public string StudentName { get; set; } = string.Empty;

        public bool CanSubmitNow { get; set; }

        public DateTime? SubmissionOpensAtUtc { get; set; }

        public DateTime? SubmissionClosesAtUtc { get; set; }

        public int ProposalCount { get; set; }

        public int PendingCount { get; set; }

        public int UnderReviewCount { get; set; }

        public int MatchedCount { get; set; }

        public int WithdrawnCount { get; set; }

        public bool HasProjectGroup { get; set; }

        public bool IsProjectGroupLead { get; set; }

        public string? ProjectGroupName { get; set; }

        public int ProjectGroupMemberCount { get; set; }

        public List<StudentProposalSummaryViewModel> RecentProposals { get; set; } = new();
    }
}
