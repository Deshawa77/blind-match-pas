using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlindMatchPAS.Models
{
    public class SupervisorInterest
    {
        public int Id { get; set; }

        [Required]
        public int ProjectProposalId { get; set; }

        [ForeignKey("ProjectProposalId")]
        public ProjectProposal? ProjectProposal { get; set; }

        [Required]
        public string SupervisorId { get; set; } = string.Empty;

        [ForeignKey("SupervisorId")]
        public ApplicationUser? Supervisor { get; set; }

        public DateTime ExpressedAt { get; set; } = DateTime.UtcNow;

        public bool IsConfirmed { get; set; } = false;
    }
}