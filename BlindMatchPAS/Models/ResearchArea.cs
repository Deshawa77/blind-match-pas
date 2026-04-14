using System.ComponentModel.DataAnnotations;

namespace BlindMatchPAS.Models
{
    public class ResearchArea
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z0-9\s\-\&]+$", ErrorMessage = "Research area name contains invalid characters.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Description { get; set; }

        public ICollection<ProjectProposal> ProjectProposals { get; set; } = new List<ProjectProposal>();
    }
}