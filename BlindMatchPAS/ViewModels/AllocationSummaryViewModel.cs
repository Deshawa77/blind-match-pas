namespace BlindMatchPAS.ViewModels
{
    public class AllocationSummaryViewModel
    {
        public int MatchId { get; set; }

        public int ProjectProposalId { get; set; }

        public string ProjectTitle { get; set; } = string.Empty;

        public string ResearchAreaName { get; set; } = string.Empty;

        public string StudentName { get; set; } = string.Empty;

        public string StudentEmail { get; set; } = string.Empty;

        public string SupervisorName { get; set; } = string.Empty;

        public string SupervisorEmail { get; set; } = string.Empty;

        public DateTime MatchedAtUtc { get; set; }
    }
}
