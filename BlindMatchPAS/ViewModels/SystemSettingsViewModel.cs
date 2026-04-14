using System.ComponentModel.DataAnnotations;

namespace BlindMatchPAS.ViewModels
{
    public class SystemSettingsViewModel
    {
        [Display(Name = "Proposal submissions open")]
        [DataType(DataType.DateTime)]
        public DateTime? ProposalSubmissionOpensAt { get; set; }

        [Display(Name = "Proposal submissions close")]
        [DataType(DataType.DateTime)]
        public DateTime? ProposalSubmissionClosesAt { get; set; }

        [Display(Name = "Matching opens")]
        [DataType(DataType.DateTime)]
        public DateTime? MatchingOpensAt { get; set; }

        [Display(Name = "Matching closes")]
        [DataType(DataType.DateTime)]
        public DateTime? MatchingClosesAt { get; set; }

        [Range(1, 20)]
        [Display(Name = "Default supervisor capacity")]
        public int DefaultSupervisorCapacity { get; set; } = 4;

        [Display(Name = "Enable email notifications")]
        public bool EmailNotificationsEnabled { get; set; } = true;

        [Display(Name = "Require confirmed email before sign-in")]
        public bool RequireConfirmedAccountToSignIn { get; set; }

        [Display(Name = "Allow optional two-factor authentication")]
        public bool AllowOptionalTwoFactor { get; set; } = true;
    }
}
