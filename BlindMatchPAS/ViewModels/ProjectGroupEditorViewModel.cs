using System.ComponentModel.DataAnnotations;

namespace BlindMatchPAS.ViewModels
{
    public class ProjectGroupEditorViewModel
    {
        public int? ProjectGroupId { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 3)]
        [Display(Name = "Group Name")]
        public string GroupName { get; set; } = string.Empty;

        public List<string> SelectedMemberIds { get; set; } = new();
    }
}
