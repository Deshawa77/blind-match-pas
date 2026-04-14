namespace BlindMatchPAS.Constants
{
    public static class ApplicationRoles
    {
        public const string Student = "Student";
        public const string Supervisor = "Supervisor";
        public const string ModuleLeader = "ModuleLeader";
        public const string Admin = "Admin";

        public static readonly string[] All = { Student, Supervisor, ModuleLeader, Admin };
        public static readonly string[] CoordinatorRoles = { ModuleLeader, Admin };
        public static readonly string[] SelfRegistrationRoles = { Student, Supervisor };
    }
}
