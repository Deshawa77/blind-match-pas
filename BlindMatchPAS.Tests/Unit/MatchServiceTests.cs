using BlindMatchPAS.Constants;
using BlindMatchPAS.Models;
using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Services;
using BlindMatchPAS.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BlindMatchPAS.Tests.Unit
{
    public class MatchServiceTests
    {
        [Fact]
        public async Task ExpressInterestAsync_TransitionsPendingProposalToUnderReview()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var student = TestDbFactory.CreateUser("student-1", "Student One", ApplicationRoles.Student);
            var supervisor = TestDbFactory.CreateUser("supervisor-1", "Supervisor One", ApplicationRoles.Supervisor);
            var researchArea = TestDbFactory.CreateResearchArea(1, "Artificial Intelligence");
            var proposal = TestDbFactory.CreateProposal(1, researchArea.Id, student.Id);

            context.Users.AddRange(student, supervisor);
            context.ResearchAreas.Add(researchArea);
            context.ProjectProposals.Add(proposal);
            await context.SaveChangesAsync();

            var userDirectory = new Mock<IUserDirectoryService>(MockBehavior.Strict);
            var settingsService = BuildSettingsServiceMock();
            var auditService = new Mock<IAuditService>();
            var notificationService = new Mock<INotificationService>();
            var service = new MatchService(context, userDirectory.Object, settingsService.Object, auditService.Object, notificationService.Object);

            var result = await service.ExpressInterestAsync(proposal.Id, supervisor.Id);

            Assert.True(result.Succeeded);
            Assert.Equal(ProposalStatuses.UnderReview, proposal.Status);
            Assert.Single(await context.SupervisorInterests.ToListAsync());
        }

        [Fact]
        public async Task ConfirmMatchAsync_CreatesMatchRecordAndRevealsIdentity()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var student = TestDbFactory.CreateUser("student-2", "Student Two", ApplicationRoles.Student);
            var supervisor = TestDbFactory.CreateUser("supervisor-2", "Supervisor Two", ApplicationRoles.Supervisor);
            var researchArea = TestDbFactory.CreateResearchArea(2, "Cybersecurity");
            var proposal = TestDbFactory.CreateProposal(2, researchArea.Id, student.Id);
            var interest = new SupervisorInterest
            {
                Id = 1,
                ProjectProposalId = proposal.Id,
                SupervisorId = supervisor.Id
            };

            context.Users.AddRange(student, supervisor);
            context.ResearchAreas.Add(researchArea);
            context.ProjectProposals.Add(proposal);
            context.SupervisorInterests.Add(interest);
            await context.SaveChangesAsync();

            var userDirectory = new Mock<IUserDirectoryService>(MockBehavior.Strict);
            var settingsService = BuildSettingsServiceMock();
            var auditService = new Mock<IAuditService>();
            var notificationService = new Mock<INotificationService>();
            var service = new MatchService(context, userDirectory.Object, settingsService.Object, auditService.Object, notificationService.Object);

            var result = await service.ConfirmMatchAsync(interest.Id, supervisor.Id);

            var match = await context.MatchRecords.SingleAsync();
            Assert.True(result.Succeeded);
            Assert.Equal(ProposalStatuses.Matched, proposal.Status);
            Assert.True(proposal.IsMatched);
            Assert.True(proposal.IsIdentityRevealed);
            Assert.Equal(proposal.Id, match.ProjectProposalId);
            Assert.Equal(supervisor.Id, match.SupervisorId);
        }

        [Fact]
        public async Task ReassignMatchAsync_RejectsUsersWhoAreNotSupervisors()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var student = TestDbFactory.CreateUser("student-3", "Student Three", ApplicationRoles.Student);
            var oldSupervisor = TestDbFactory.CreateUser("supervisor-3", "Supervisor Three", ApplicationRoles.Supervisor);
            var invalidReplacement = TestDbFactory.CreateUser("admin-1", "Coordinator", ApplicationRoles.Admin);
            var researchArea = TestDbFactory.CreateResearchArea(3, "Cloud Computing");
            var proposal = TestDbFactory.CreateProposal(3, researchArea.Id, student.Id);
            proposal.Status = ProposalStatuses.Matched;
            proposal.IsMatched = true;
            proposal.IsIdentityRevealed = true;

            var match = new MatchRecord
            {
                Id = 1,
                ProjectProposalId = proposal.Id,
                StudentId = student.Id,
                SupervisorId = oldSupervisor.Id
            };

            context.Users.AddRange(student, oldSupervisor, invalidReplacement);
            context.ResearchAreas.Add(researchArea);
            context.ProjectProposals.Add(proposal);
            context.MatchRecords.Add(match);
            await context.SaveChangesAsync();

            var userDirectory = new Mock<IUserDirectoryService>();
            userDirectory
                .Setup(service => service.FindByIdAsync(invalidReplacement.Id))
                .ReturnsAsync(invalidReplacement);
            userDirectory
                .Setup(service => service.IsInRoleAsync(invalidReplacement, ApplicationRoles.Supervisor))
                .ReturnsAsync(false);

            var settingsService = BuildSettingsServiceMock();
            var auditService = new Mock<IAuditService>();
            var notificationService = new Mock<INotificationService>();
            var service = new MatchService(context, userDirectory.Object, settingsService.Object, auditService.Object, notificationService.Object);

            var result = await service.ReassignMatchAsync(match.Id, invalidReplacement.Id);

            Assert.False(result.Succeeded);
            Assert.Equal(MatchOperationStatus.InvalidState, result.Status);
            Assert.Equal(oldSupervisor.Id, (await context.MatchRecords.SingleAsync()).SupervisorId);
        }

        [Fact]
        public async Task ConfirmMatchAsync_RejectsWhenSupervisorCapacityIsReached()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var student = TestDbFactory.CreateUser("student-4", "Student Four", ApplicationRoles.Student);
            var supervisor = TestDbFactory.CreateUser("supervisor-4", "Supervisor Four", ApplicationRoles.Supervisor);
            supervisor.SupervisorCapacity = 1;
            var otherStudent = TestDbFactory.CreateUser("student-5", "Student Five", ApplicationRoles.Student);
            var researchArea = TestDbFactory.CreateResearchArea(4, "Machine Learning");
            var existingProposal = TestDbFactory.CreateProposal(4, researchArea.Id, otherStudent.Id);
            existingProposal.Status = ProposalStatuses.Matched;
            existingProposal.IsMatched = true;
            existingProposal.IsIdentityRevealed = true;
            var existingMatch = new MatchRecord
            {
                Id = 4,
                ProjectProposalId = existingProposal.Id,
                StudentId = otherStudent.Id,
                SupervisorId = supervisor.Id
            };

            var proposal = TestDbFactory.CreateProposal(5, researchArea.Id, student.Id);
            var interest = new SupervisorInterest
            {
                Id = 5,
                ProjectProposalId = proposal.Id,
                SupervisorId = supervisor.Id
            };

            context.Users.AddRange(student, supervisor, otherStudent);
            context.ResearchAreas.Add(researchArea);
            context.ProjectProposals.AddRange(existingProposal, proposal);
            context.MatchRecords.Add(existingMatch);
            context.SupervisorInterests.Add(interest);
            await context.SaveChangesAsync();

            var userDirectory = new Mock<IUserDirectoryService>(MockBehavior.Strict);
            var settingsService = BuildSettingsServiceMock();
            settingsService
                .Setup(service => service.GetSupervisorCapacityAsync(supervisor))
                .ReturnsAsync(1);
            var auditService = new Mock<IAuditService>();
            var notificationService = new Mock<INotificationService>();
            var service = new MatchService(context, userDirectory.Object, settingsService.Object, auditService.Object, notificationService.Object);

            var result = await service.ConfirmMatchAsync(interest.Id, supervisor.Id);

            Assert.False(result.Succeeded);
            Assert.Equal(MatchOperationStatus.InvalidState, result.Status);
            Assert.Single(await context.MatchRecords.ToListAsync());
        }

        private static Mock<ISystemSettingsService> BuildSettingsServiceMock()
        {
            var settingsService = new Mock<ISystemSettingsService>();
            settingsService.Setup(service => service.IsMatchingOpenAsync(It.IsAny<DateTime>())).ReturnsAsync(true);
            settingsService.Setup(service => service.GetSettingsAsync()).ReturnsAsync(new Models.SystemSettings
            {
                DefaultSupervisorCapacity = 4,
                EmailNotificationsEnabled = false
            });
            settingsService.Setup(service => service.GetSupervisorCapacityAsync(It.IsAny<ApplicationUser>())).ReturnsAsync((ApplicationUser supervisor) => supervisor.SupervisorCapacity ?? 4);
            return settingsService;
        }
    }
}
