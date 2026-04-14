namespace BlindMatchPAS.ViewModels
{
    public class StudentProposalSummaryViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string ResearchAreaName { get; set; } = string.Empty;

        public bool IsGroupProposal { get; set; }

        public string OwnershipLabel { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public bool IsMatched { get; set; }

        public bool IsIdentityRevealed { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? SupervisorFullName { get; set; }

        public string? SupervisorEmail { get; set; }

        public DateTime? MatchedAt { get; set; }

        public bool CanEdit { get; set; }

        public bool CanWithdraw { get; set; }

        public bool CanManageOwnership { get; set; }
    }
}
