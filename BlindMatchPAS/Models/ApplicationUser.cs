using Microsoft.AspNetCore.Identity;

namespace BlindMatchPAS.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string RoleType { get; set; } = string.Empty;

        public ICollection<ProjectProposal> StudentProjectProposals { get; set; } = new List<ProjectProposal>();
        public ICollection<SupervisorInterest> SupervisorInterests { get; set; } = new List<SupervisorInterest>();
        public ICollection<MatchRecord> StudentMatches { get; set; } = new List<MatchRecord>();
        public ICollection<MatchRecord> SupervisorMatches { get; set; } = new List<MatchRecord>();
        public ICollection<SupervisorExpertise> SupervisorExpertiseAreas { get; set; } = new List<SupervisorExpertise>();
    }
}