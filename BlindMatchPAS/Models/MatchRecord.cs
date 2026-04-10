using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlindMatchPAS.Models
{
    public class MatchRecord
    {
        public int Id { get; set; }

        [Required]
        public int ProjectProposalId { get; set; }

        [ForeignKey("ProjectProposalId")]
        public ProjectProposal? ProjectProposal { get; set; }

        [Required]
        public string StudentId { get; set; } = string.Empty;

        [ForeignKey("StudentId")]
        public ApplicationUser? Student { get; set; }

        [Required]
        public string SupervisorId { get; set; } = string.Empty;

        [ForeignKey("SupervisorId")]
        public ApplicationUser? Supervisor { get; set; }

        public DateTime MatchedAt { get; set; } = DateTime.UtcNow;

        public bool IdentityRevealed { get; set; } = true;
    }
}