using BlindMatchPAS.Constants;
using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Services;
using BlindMatchPAS.Tests.Helpers;
using BlindMatchPAS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BlindMatchPAS.Tests.Integration
{
    public class StudentServiceTests
    {
        [Fact]
        public async Task CreateProposalAsync_PersistsBlindProposalInPendingState()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);
            var settingsService = new Mock<ISystemSettingsService>();
            settingsService.Setup(service => service.IsProposalSubmissionOpenAsync(It.IsAny<DateTime>())).ReturnsAsync(true);
            var auditService = new Mock<IAuditService>();
            var service = new StudentService(context, settingsService.Object, auditService.Object);

            var student = TestDbFactory.CreateUser("student-a", "Student Alpha", ApplicationRoles.Student);
            var researchArea = TestDbFactory.CreateResearchArea(10, "Data Science");
            context.Users.Add(student);
            context.ResearchAreas.Add(researchArea);
            await context.SaveChangesAsync();

            var created = await service.CreateProposalAsync(student.Id, new ProjectProposalViewModel
            {
                Title = "Predictive Analytics Platform",
                Abstract = new string('B', 80),
                TechnicalStack = "Python, FastAPI, SQL",
                ResearchAreaId = researchArea.Id
            });

            var proposal = await context.ProjectProposals.SingleAsync();
            Assert.True(created);
            Assert.Equal(ProposalStatuses.Pending, proposal.Status);
            Assert.False(proposal.IsMatched);
            Assert.False(proposal.IsIdentityRevealed);
        }

        [Fact]
        public async Task WithdrawProposalAsync_UpdatesStatusWhenProposalIsEligible()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);
            var settingsService = new Mock<ISystemSettingsService>();
            settingsService.Setup(service => service.IsProposalSubmissionOpenAsync(It.IsAny<DateTime>())).ReturnsAsync(true);
            var auditService = new Mock<IAuditService>();
            var service = new StudentService(context, settingsService.Object, auditService.Object);

            var student = TestDbFactory.CreateUser("student-b", "Student Beta", ApplicationRoles.Student);
            var researchArea = TestDbFactory.CreateResearchArea(11, "Web Development");
            var proposal = TestDbFactory.CreateProposal(11, researchArea.Id, student.Id);

            context.Users.Add(student);
            context.ResearchAreas.Add(researchArea);
            context.ProjectProposals.Add(proposal);
            await context.SaveChangesAsync();

            var withdrawn = await service.WithdrawProposalAsync(proposal.Id, proposal.StudentId);

            Assert.True(withdrawn);
            Assert.Equal(ProposalStatuses.Withdrawn, (await context.ProjectProposals.SingleAsync()).Status);
        }
    }
}
