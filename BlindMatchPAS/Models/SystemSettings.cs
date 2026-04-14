using System.ComponentModel.DataAnnotations;

namespace BlindMatchPAS.Models
{
    public class SystemSettings
    {
        public int Id { get; set; } = 1;

        public DateTime? ProposalSubmissionOpensAtUtc { get; set; }

        public DateTime? ProposalSubmissionClosesAtUtc { get; set; }

        public DateTime? MatchingOpensAtUtc { get; set; }

        public DateTime? MatchingClosesAtUtc { get; set; }

        [Range(1, 20)]
        public int DefaultSupervisorCapacity { get; set; } = 4;

        public bool EmailNotificationsEnabled { get; set; } = true;

        public bool RequireConfirmedAccountToSignIn { get; set; }

        public bool AllowSelfRegistration { get; set; }

        public bool AllowOptionalTwoFactor { get; set; } = true;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        [StringLength(450)]
        public string? UpdatedByUserId { get; set; }
    }
}
