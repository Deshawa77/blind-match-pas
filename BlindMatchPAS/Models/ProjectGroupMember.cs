using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlindMatchPAS.Models
{
    public class ProjectGroupMember
    {
        public int Id { get; set; }

        [Required]
        public int ProjectGroupId { get; set; }

        [ForeignKey(nameof(ProjectGroupId))]
        public ProjectGroup? ProjectGroup { get; set; }

        [Required]
        [StringLength(450)]
        public string StudentId { get; set; } = string.Empty;

        [ForeignKey(nameof(StudentId))]
        public ApplicationUser? Student { get; set; }

        public bool IsLead { get; set; }

        public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
