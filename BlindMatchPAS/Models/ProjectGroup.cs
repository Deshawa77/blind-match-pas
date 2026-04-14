using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlindMatchPAS.Models
{
    public class ProjectGroup
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z0-9\s\-\&]+$", ErrorMessage = "Group name contains invalid characters.")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(450)]
        public string LeadStudentId { get; set; } = string.Empty;

        [ForeignKey(nameof(LeadStudentId))]
        public ApplicationUser? LeadStudent { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAtUtc { get; set; }

        public ICollection<ProjectGroupMember> Members { get; set; } = new List<ProjectGroupMember>();

        public ICollection<ProjectProposal> ProjectProposals { get; set; } = new List<ProjectProposal>();
    }
}
