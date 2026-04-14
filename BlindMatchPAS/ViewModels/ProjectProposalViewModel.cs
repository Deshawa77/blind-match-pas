using BlindMatchPAS.Constants;
using System.ComponentModel.DataAnnotations;

namespace BlindMatchPAS.ViewModels
{
    public class ProjectProposalViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        [Display(Name = "Project Title")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2000, MinimumLength = 50)]
        [Display(Name = "Abstract")]
        public string Abstract { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        [Display(Name = "Technical Stack")]
        public string TechnicalStack { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Research Area")]
        public int ResearchAreaId { get; set; }

        [Required]
        [Display(Name = "Submission Type")]
        [RegularExpression("^(Individual|Group)$", ErrorMessage = "Submission type must be Individual or Group.")]
        public string OwnershipType { get; set; } = ProposalOwnershipTypes.Individual;

        [Display(Name = "Project Group")]
        public int? ProjectGroupId { get; set; }

        public bool CanSubmitAsGroup { get; set; }

        public string? SelectedGroupName { get; set; }
    }
}
