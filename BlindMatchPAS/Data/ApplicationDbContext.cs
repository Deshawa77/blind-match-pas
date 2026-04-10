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
        public DbSet<SupervisorInterest> SupervisorInterests { get; set; }
        public DbSet<MatchRecord> MatchRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ProjectProposal>()
                .HasOne(p => p.Student)
                .WithMany(u => u.StudentProjectProposals)
                .HasForeignKey(p => p.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SupervisorInterest>()
                .HasOne(si => si.Supervisor)
                .WithMany(u => u.SupervisorInterests)
                .HasForeignKey(si => si.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

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
        }
    }
}