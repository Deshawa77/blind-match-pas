namespace BlindMatchPAS.ViewModels
{
    public class SupervisorBrowseProjectsViewModel
    {
        public int? ResearchAreaId { get; set; }

        public string SearchTerm { get; set; } = string.Empty;

        public bool HasConfiguredExpertise { get; set; }

        public bool IsMatchingWindowOpen { get; set; }

        public DateTime? MatchingOpensAtUtc { get; set; }

        public DateTime? MatchingClosesAtUtc { get; set; }

        public int CapacityLimit { get; set; }

        public int ConfirmedMatchCount { get; set; }

        public int RemainingCapacity { get; set; }

        public List<SupervisorProjectCardViewModel> Projects { get; set; } = new();
    }
}
