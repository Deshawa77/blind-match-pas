using BlindMatchPAS.Constants;

namespace BlindMatchPAS.ViewModels
{
    public class ProjectGroupWorkspaceViewModel
    {
        public bool HasGroup { get; set; }

        public bool IsLead { get; set; }

        public string StudentName { get; set; } = string.Empty;

        public string? GroupName { get; set; }

        public string? LeadName { get; set; }

        public int MemberCount { get; set; }

        public bool GroupLockedByProposalActivity { get; set; }

        public int MaxMembers { get; set; } = ProjectGroupRules.MaxMembers;

        public ProjectGroupEditorViewModel Editor { get; set; } = new();

        public List<ProjectGroupMemberOptionViewModel> Members { get; set; } = new();

        public List<ProjectGroupMemberOptionViewModel> AvailableStudents { get; set; } = new();
    }
}
