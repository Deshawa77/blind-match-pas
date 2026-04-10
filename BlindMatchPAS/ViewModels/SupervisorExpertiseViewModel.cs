using System.ComponentModel.DataAnnotations;

namespace BlindMatchPAS.ViewModels
{
    public class SupervisorExpertiseViewModel
    {
        [Display(Name = "Preferred Research Areas")]
        public List<int> SelectedResearchAreaIds { get; set; } = new();
    }
}