using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlindMatchPAS.Models
{
    public class ProjectProposal
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150, MinimumLength = 5)]
        [RegularExpression(@"^[a-zA-Z0-9\s\-\:\,\.\(\)]+$", ErrorMessage = "Title contains invalid characters.")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2000, MinimumLength = 50)]
        public string Abstract { get; set; } = string.Empty;

        [Required]
        [StringLength(500, MinimumLength = 2)]
        [Display(Name = "Technical Stack")]
        public string TechnicalStack { get; set; } = string.Empty;

        [Required]
        public int ResearchAreaId { get; set; }

        [ForeignKey("ResearchAreaId")]
        public ResearchArea? ResearchArea { get; set; }

        [Required]
        public string StudentId { get; set; } = string.Empty;

        [ForeignKey("StudentId")]
        public ApplicationUser? Student { get; set; }

        public int? ProjectGroupId { get; set; }

        [ForeignKey(nameof(ProjectGroupId))]
        public ProjectGroup? ProjectGroup { get; set; }

        [Required]
        [StringLength(30)]
        [RegularExpression("^(Pending|UnderReview|Matched|Withdrawn)$",
            ErrorMessage = "Status must be Pending, UnderReview, Matched, or Withdrawn.")]
        public string Status { get; set; } = "Pending";

        public bool IsMatched { get; set; } = false;

        public bool IsIdentityRevealed { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<SupervisorInterest> SupervisorInterests { get; set; } = new List<SupervisorInterest>();
        public ICollection<MatchRecord> MatchRecords { get; set; } = new List<MatchRecord>();
    }
}
