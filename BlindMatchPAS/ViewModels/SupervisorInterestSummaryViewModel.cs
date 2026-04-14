namespace BlindMatchPAS.ViewModels
{
    public class SupervisorInterestSummaryViewModel
    {
        public int InterestId { get; set; }

        public int ProposalId { get; set; }

        public string ProjectTitle { get; set; } = string.Empty;

        public string ResearchAreaName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime ExpressedAt { get; set; }

        public bool IsConfirmed { get; set; }

        public bool CanConfirm { get; set; }
    }
}
