using BlindMatchPAS.Constants;
using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Models;
using BlindMatchPAS.Services;
using BlindMatchPAS.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BlindMatchPAS.Tests.Unit
{
    public class MatchServiceEdgeCaseTests
    {
        [Fact]
        public async Task ExpressInterestAsync_RejectsWhenMatchingWindowIsClosed()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var student = TestDbFactory.CreateUser("student-closed", "Closed Student", ApplicationRoles.Student);
            var supervisor = TestDbFactory.CreateUser("supervisor-closed", "Closed Supervisor", ApplicationRoles.Supervisor);
            var researchArea = TestDbFactory.CreateResearchArea(30, "Software Engineering");
            var proposal = TestDbFactory.CreateProposal(30, researchArea.Id, student.Id);

            context.Users.AddRange(student, supervisor);
            context.ResearchAreas.Add(researchArea);
            context.ProjectProposals.Add(proposal);
            await context.SaveChangesAsync();

            var settingsService = BuildSettingsServiceMock(isMatchingOpen: false);
            var service = new MatchService(context, Mock.Of<IUserDirectoryService>(), settingsService.Object, Mock.Of<IAuditService>(), Mock.Of<INotificationService>());

            var result = await service.ExpressInterestAsync(proposal.Id, supervisor.Id);

            Assert.False(result.Succeeded);
            Assert.Equal(MatchOperationStatus.InvalidState, result.Status);
            Assert.Equal("Pending", (await context.ProjectProposals.SingleAsync()).Status);
        }

        [Fact]
        public async Task ExpressInterestAsync_RejectsSupervisorOutsideExpertise()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var student = TestDbFactory.CreateUser("student-exp", "Expertise Student", ApplicationRoles.Student);
            var supervisor = TestDbFactory.CreateUser("supervisor-exp", "Expertise Supervisor", ApplicationRoles.Supervisor);
            var proposalArea = TestDbFactory.CreateResearchArea(31, "Artificial Intelligence");
            var expertiseArea = TestDbFactory.CreateResearchArea(32, "Cybersecurity");
            var proposal = TestDbFactory.CreateProposal(31, proposalArea.Id, student.Id);

            context.Users.AddRange(student, supervisor);
            context.ResearchAreas.AddRange(proposalArea, expertiseArea);
            context.ProjectProposals.Add(proposal);
            context.SupervisorExpertise.Add(new SupervisorExpertise
            {
                SupervisorId = supervisor.Id,
                ResearchAreaId = expertiseArea.Id
            });
            await context.SaveChangesAsync();

            var settingsService = BuildSettingsServiceMock();
            var service = new MatchService(context, Mock.Of<IUserDirectoryService>(), settingsService.Object, Mock.Of<IAuditService>(), Mock.Of<INotificationService>());

            var result = await service.ExpressInterestAsync(proposal.Id, supervisor.Id);

            Assert.False(result.Succeeded);
            Assert.Equal(MatchOperationStatus.Forbidden, result.Status);
            Assert.Empty(await context.SupervisorInterests.ToListAsync());
        }

        [Fact]
        public async Task ExpressInterestAsync_RejectsDuplicateInterest()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var student = TestDbFactory.CreateUser("student-dup", "Duplicate Student", ApplicationRoles.Student);
            var supervisor = TestDbFactory.CreateUser("supervisor-dup", "Duplicate Supervisor", ApplicationRoles.Supervisor);
            var researchArea = TestDbFactory.CreateResearchArea(33, "Data Engineering");
            var proposal = TestDbFactory.CreateProposal(33, researchArea.Id, student.Id);

            context.Users.AddRange(student, supervisor);
            context.ResearchAreas.Add(researchArea);
            context.ProjectProposals.Add(proposal);
            context.SupervisorInterests.Add(new SupervisorInterest
            {
                ProjectProposalId = proposal.Id,
                SupervisorId = supervisor.Id
            });
            await context.SaveChangesAsync();

            var settingsService = BuildSettingsServiceMock();
            var service = new MatchService(context, Mock.Of<IUserDirectoryService>(), settingsService.Object, Mock.Of<IAuditService>(), Mock.Of<INotificationService>());

            var result = await service.ExpressInterestAsync(proposal.Id, supervisor.Id);

            Assert.False(result.Succeeded);
            Assert.Equal(MatchOperationStatus.InvalidState, result.Status);
            Assert.Single(await context.SupervisorInterests.ToListAsync());
        }

        [Fact]
        public async Task ConfirmMatchAsync_RejectsAlreadyMatchedProposal()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var student = TestDbFactory.CreateUser("student-matched", "Matched Student", ApplicationRoles.Student);
            var supervisor = TestDbFactory.CreateUser("supervisor-matched", "Matched Supervisor", ApplicationRoles.Supervisor);
            var researchArea = TestDbFactory.CreateResearchArea(34, "Cloud");
            var proposal = TestDbFactory.CreateProposal(34, researchArea.Id, student.Id);
            proposal.Status = ProposalStatuses.Matched;
            proposal.IsMatched = true;
            proposal.IsIdentityRevealed = true;

            var interest = new SupervisorInterest
            {
                Id = 34,
                ProjectProposalId = proposal.Id,
                SupervisorId = supervisor.Id
            };

            context.Users.AddRange(student, supervisor);
            context.ResearchAreas.Add(researchArea);
            context.ProjectProposals.Add(proposal);
            context.SupervisorInterests.Add(interest);
            await context.SaveChangesAsync();

            var settingsService = BuildSettingsServiceMock();
            var service = new MatchService(context, Mock.Of<IUserDirectoryService>(), settingsService.Object, Mock.Of<IAuditService>(), Mock.Of<INotificationService>());

            var result = await service.ConfirmMatchAsync(interest.Id, supervisor.Id);

            Assert.False(result.Succeeded);
            Assert.Equal(MatchOperationStatus.InvalidState, result.Status);
            Assert.Empty(await context.MatchRecords.ToListAsync());
        }

        [Fact]
        public async Task ReassignMatchAsync_RejectsSupervisorWithoutRequiredExpertise()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var student = TestDbFactory.CreateUser("student-reassign", "Reassign Student", ApplicationRoles.Student);
            var oldSupervisor = TestDbFactory.CreateUser("supervisor-old", "Old Supervisor", ApplicationRoles.Supervisor);
            var newSupervisor = TestDbFactory.CreateUser("supervisor-new", "New Supervisor", ApplicationRoles.Supervisor);
            var proposalArea = TestDbFactory.CreateResearchArea(35, "Machine Learning");
            var mismatchedArea = TestDbFactory.CreateResearchArea(36, "Embedded Systems");
            var proposal = TestDbFactory.CreateProposal(35, proposalArea.Id, student.Id);
            proposal.Status = ProposalStatuses.Matched;
            proposal.IsMatched = true;
            proposal.IsIdentityRevealed = true;

            var match = new MatchRecord
            {
                Id = 35,
                ProjectProposalId = proposal.Id,
                StudentId = student.Id,
                SupervisorId = oldSupervisor.Id
            };

            context.Users.AddRange(student, oldSupervisor, newSupervisor);
            context.ResearchAreas.AddRange(proposalArea, mismatchedArea);
            context.ProjectProposals.Add(proposal);
            context.MatchRecords.Add(match);
            context.SupervisorExpertise.Add(new SupervisorExpertise
            {
                SupervisorId = newSupervisor.Id,
                ResearchAreaId = mismatchedArea.Id
            });
            await context.SaveChangesAsync();

            var userDirectory = new Mock<IUserDirectoryService>();
            userDirectory.Setup(service => service.FindByIdAsync(newSupervisor.Id)).ReturnsAsync(newSupervisor);
            userDirectory.Setup(service => service.IsInRoleAsync(newSupervisor, ApplicationRoles.Supervisor)).ReturnsAsync(true);

            var settingsService = BuildSettingsServiceMock();
            var service = new MatchService(context, userDirectory.Object, settingsService.Object, Mock.Of<IAuditService>(), Mock.Of<INotificationService>());

            var result = await service.ReassignMatchAsync(match.Id, newSupervisor.Id);

            Assert.False(result.Succeeded);
            Assert.Equal(MatchOperationStatus.Forbidden, result.Status);
            Assert.Equal(oldSupervisor.Id, (await context.MatchRecords.SingleAsync()).SupervisorId);
        }

        private static Mock<ISystemSettingsService> BuildSettingsServiceMock(bool isMatchingOpen = true)
        {
            var settingsService = new Mock<ISystemSettingsService>();
            settingsService.Setup(service => service.IsMatchingOpenAsync(It.IsAny<DateTime>())).ReturnsAsync(isMatchingOpen);
            settingsService.Setup(service => service.GetSettingsAsync()).ReturnsAsync(new SystemSettings
            {
                DefaultSupervisorCapacity = 4,
                EmailNotificationsEnabled = false
            });
            settingsService.Setup(service => service.GetSupervisorCapacityAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync((ApplicationUser supervisor) => supervisor.SupervisorCapacity ?? 4);
            return settingsService;
        }
    }
}
