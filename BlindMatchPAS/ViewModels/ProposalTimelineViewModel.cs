namespace BlindMatchPAS.ViewModels
{
    public class ProposalTimelineViewModel
    {
        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? MatchedAt { get; set; }

        public bool IsIdentityRevealed { get; set; }
    }
}
