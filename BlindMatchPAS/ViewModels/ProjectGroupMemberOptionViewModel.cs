namespace BlindMatchPAS.ViewModels
{
    public class ProjectGroupMemberOptionViewModel
    {
        public string StudentId { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public bool IsLead { get; set; }

        public bool Selected { get; set; }
    }
}
