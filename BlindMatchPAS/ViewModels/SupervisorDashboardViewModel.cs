namespace BlindMatchPAS.ViewModels
{
    public class SupervisorDashboardViewModel
    {
        public string SupervisorName { get; set; } = string.Empty;

        public bool IsMatchingWindowOpen { get; set; }

        public DateTime? MatchingOpensAtUtc { get; set; }

        public DateTime? MatchingClosesAtUtc { get; set; }

        public int ExpertiseCount { get; set; }

        public int AvailableProjectCount { get; set; }

        public int ConfirmedMatchCount { get; set; }

        public int CapacityLimit { get; set; }

        public int RemainingCapacity { get; set; }

        public List<string> ExpertiseAreas { get; set; } = new();

        public List<SupervisorInterestSummaryViewModel> PendingInterests { get; set; } = new();
    }
}
