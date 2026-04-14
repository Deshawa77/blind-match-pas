using BlindMatchPAS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ResearchArea> ResearchAreas { get; set; }
        public DbSet<ProjectProposal> ProjectProposals { get; set; }
        public DbSet<ProjectGroup> ProjectGroups { get; set; }
        public DbSet<ProjectGroupMember> ProjectGroupMembers { get; set; }
        public DbSet<SupervisorInterest> SupervisorInterests { get; set; }
        public DbSet<MatchRecord> MatchRecords { get; set; }
        public DbSet<SupervisorExpertise> SupervisorExpertise { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<SystemSettings> SystemSettings { get; set; }
        public DbSet<NotificationEmail> NotificationEmails { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ProjectProposal>()
                .HasOne(p => p.Student)
                .WithMany(u => u.StudentProjectProposals)
                .HasForeignKey(p => p.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProjectProposal>()
                .HasOne(projectProposal => projectProposal.ProjectGroup)
                .WithMany(projectGroup => projectGroup.ProjectProposals)
                .HasForeignKey(projectProposal => projectProposal.ProjectGroupId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ProjectGroup>()
                .HasIndex(projectGroup => projectGroup.LeadStudentId)
                .IsUnique();

            builder.Entity<ProjectGroup>()
                .HasOne(projectGroup => projectGroup.LeadStudent)
                .WithMany(applicationUser => applicationUser.LedProjectGroups)
                .HasForeignKey(projectGroup => projectGroup.LeadStudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProjectGroupMember>()
                .HasIndex(projectGroupMember => new { projectGroupMember.ProjectGroupId, projectGroupMember.StudentId })
                .IsUnique();

            builder.Entity<ProjectGroupMember>()
                .HasIndex(projectGroupMember => projectGroupMember.StudentId)
                .IsUnique();

            builder.Entity<ProjectGroupMember>()
                .HasOne(projectGroupMember => projectGroupMember.ProjectGroup)
                .WithMany(projectGroup => projectGroup.Members)
                .HasForeignKey(projectGroupMember => projectGroupMember.ProjectGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ProjectGroupMember>()
                .HasOne(projectGroupMember => projectGroupMember.Student)
                .WithMany(applicationUser => applicationUser.ProjectGroupMemberships)
                .HasForeignKey(projectGroupMember => projectGroupMember.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ResearchArea>()
                .HasIndex(researchArea => researchArea.Name)
                .IsUnique();

            builder.Entity<SupervisorInterest>()
                .HasIndex(supervisorInterest => new { supervisorInterest.SupervisorId, supervisorInterest.ProjectProposalId })
                .IsUnique();

            builder.Entity<SupervisorInterest>()
                .HasOne(si => si.Supervisor)
                .WithMany(u => u.SupervisorInterests)
                .HasForeignKey(si => si.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MatchRecord>()
                .HasIndex(matchRecord => matchRecord.ProjectProposalId)
                .IsUnique();

            builder.Entity<MatchRecord>()
                .HasOne(m => m.Student)
                .WithMany(u => u.StudentMatches)
                .HasForeignKey(m => m.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MatchRecord>()
                .HasOne(m => m.Supervisor)
                .WithMany(u => u.SupervisorMatches)
                .HasForeignKey(m => m.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SupervisorExpertise>()
                .HasIndex(supervisorExpertise => new { supervisorExpertise.SupervisorId, supervisorExpertise.ResearchAreaId })
                .IsUnique();

            builder.Entity<SupervisorExpertise>()
                .HasOne(se => se.Supervisor)
                .WithMany(u => u.SupervisorExpertiseAreas)
                .HasForeignKey(se => se.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AuditLog>()
                .HasIndex(auditLog => auditLog.OccurredAtUtc);

            builder.Entity<SystemSettings>()
                .Property(systemSettings => systemSettings.Id)
                .ValueGeneratedNever();

            builder.Entity<NotificationEmail>()
                .HasIndex(notificationEmail => notificationEmail.CreatedAtUtc);
        }
    }
}
