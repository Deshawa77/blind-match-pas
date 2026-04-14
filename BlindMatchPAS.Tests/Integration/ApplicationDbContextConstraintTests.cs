using BlindMatchPAS.Constants;
using BlindMatchPAS.Models;
using BlindMatchPAS.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Tests.Integration
{
    public class ApplicationDbContextConstraintTests
    {
        [Fact]
        public async Task SavingDuplicateSupervisorInterest_ForSameProposalAndSupervisor_ThrowsDbUpdateException()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var student = TestDbFactory.CreateUser("student-constraint", "Constraint Student", ApplicationRoles.Student);
            var supervisor = TestDbFactory.CreateUser("supervisor-constraint", "Constraint Supervisor", ApplicationRoles.Supervisor);
            var researchArea = TestDbFactory.CreateResearchArea(20, "DevOps");
            var proposal = TestDbFactory.CreateProposal(20, researchArea.Id, student.Id);

            context.Users.AddRange(student, supervisor);
            context.ResearchAreas.Add(researchArea);
            context.ProjectProposals.Add(proposal);
            context.SupervisorInterests.Add(new SupervisorInterest
            {
                ProjectProposalId = proposal.Id,
                SupervisorId = supervisor.Id
            });
            await context.SaveChangesAsync();

            context.SupervisorInterests.Add(new SupervisorInterest
            {
                ProjectProposalId = proposal.Id,
                SupervisorId = supervisor.Id
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }
}
