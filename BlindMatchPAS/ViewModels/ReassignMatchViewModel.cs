using System.ComponentModel.DataAnnotations;

namespace BlindMatchPAS.ViewModels
{
    public class ReassignMatchViewModel
    {
        public int MatchId { get; set; }

        public int ProjectProposalId { get; set; }

        public string CurrentSupervisorId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "New Supervisor")]
        public string NewSupervisorId { get; set; } = string.Empty;
    }
}