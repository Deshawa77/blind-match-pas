using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace BlindMatchPAS.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        [RegularExpression("^(Student|Supervisor|ModuleLeader|Admin)$",
            ErrorMessage = "RoleType must be Student, Supervisor, ModuleLeader, or Admin.")]
        public string RoleType { get; set; } = string.Empty;

        [Range(1, 20)]
        [Display(Name = "Supervisor Capacity")]
        public int? SupervisorCapacity { get; set; }

        public ICollection<ProjectProposal> StudentProjectProposals { get; set; } = new List<ProjectProposal>();
        public ICollection<ProjectGroup> LedProjectGroups { get; set; } = new List<ProjectGroup>();
        public ICollection<ProjectGroupMember> ProjectGroupMemberships { get; set; } = new List<ProjectGroupMember>();
        public ICollection<SupervisorInterest> SupervisorInterests { get; set; } = new List<SupervisorInterest>();
        public ICollection<MatchRecord> StudentMatches { get; set; } = new List<MatchRecord>();
        public ICollection<MatchRecord> SupervisorMatches { get; set; } = new List<MatchRecord>();
        public ICollection<SupervisorExpertise> SupervisorExpertiseAreas { get; set; } = new List<SupervisorExpertise>();
    }
}
