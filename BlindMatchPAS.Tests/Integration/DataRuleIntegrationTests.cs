using BlindMatchPAS.Constants;
using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Models;
using BlindMatchPAS.Services;
using BlindMatchPAS.Tests.Helpers;
using BlindMatchPAS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BlindMatchPAS.Tests.Integration
{
    public class DataRuleIntegrationTests
    {
        [Fact]
        public async Task CreateProposalAsync_PersistsGroupOwnedProposal_ForGroupLead()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var settingsService = new Mock<ISystemSettingsService>();
            settingsService.Setup(service => service.IsProposalSubmissionOpenAsync(It.IsAny<DateTime>())).ReturnsAsync(true);
            var service = new StudentService(context, settingsService.Object, Mock.Of<IAuditService>());

            var lead = TestDbFactory.CreateUser("student-lead", "Lead Student", ApplicationRoles.Student);
            var member = TestDbFactory.CreateUser("student-member", "Member Student", ApplicationRoles.Student);
            var researchArea = TestDbFactory.CreateResearchArea(40, "Human Computer Interaction");
            var group = new ProjectGroup
            {
                Name = "Interaction Lab",
                LeadStudentId = lead.Id,
                Members = new List<ProjectGroupMember>
                {
                    new() { StudentId = lead.Id, IsLead = true },
                    new() { StudentId = member.Id, IsLead = false }
                }
            };

            context.Users.AddRange(lead, member);
            context.ResearchAreas.Add(researchArea);
            context.ProjectGroups.Add(group);
            await context.SaveChangesAsync();

            var created = await service.CreateProposalAsync(lead.Id, new ProjectProposalViewModel
            {
                Title = "Collaborative UX Evaluation Platform",
                Abstract = new string('G', 90),
                TechnicalStack = "ASP.NET Core, SQL Server, Playwright",
                ResearchAreaId = researchArea.Id,
                OwnershipType = ProposalOwnershipTypes.Group,
                ProjectGroupId = group.Id
            });

            var proposal = await context.ProjectProposals.SingleAsync();
            Assert.True(created);
            Assert.Equal(group.Id, proposal.ProjectGroupId);
            Assert.Equal(lead.Id, proposal.StudentId);
        }

        [Fact]
        public async Task WithdrawProposalAsync_ReturnsFalse_ForMatchedProposal()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var settingsService = new Mock<ISystemSettingsService>();
            settingsService.Setup(service => service.IsProposalSubmissionOpenAsync(It.IsAny<DateTime>())).ReturnsAsync(true);
            var service = new StudentService(context, settingsService.Object, Mock.Of<IAuditService>());

            var student = TestDbFactory.CreateUser("student-withdraw", "Matched Student", ApplicationRoles.Student);
            var researchArea = TestDbFactory.CreateResearchArea(41, "Networks");
            var proposal = TestDbFactory.CreateProposal(41, researchArea.Id, student.Id);
            proposal.Status = ProposalStatuses.Matched;
            proposal.IsMatched = true;

            context.Users.Add(student);
            context.ResearchAreas.Add(researchArea);
            context.ProjectProposals.Add(proposal);
            await context.SaveChangesAsync();

            var withdrawn = await service.WithdrawProposalAsync(proposal.Id, student.Id);

            Assert.False(withdrawn);
            Assert.Equal(ProposalStatuses.Matched, (await context.ProjectProposals.SingleAsync()).Status);
        }

        [Fact]
        public async Task SavingDuplicateMatchRecord_ForSameProposal_ThrowsDbUpdateException()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var student = TestDbFactory.CreateUser("student-match", "Constraint Student", ApplicationRoles.Student);
            var supervisor = TestDbFactory.CreateUser("supervisor-match", "Constraint Supervisor", ApplicationRoles.Supervisor);
            var researchArea = TestDbFactory.CreateResearchArea(42, "DevSecOps");
            var proposal = TestDbFactory.CreateProposal(42, researchArea.Id, student.Id);

            context.Users.AddRange(student, supervisor);
            context.ResearchAreas.Add(researchArea);
            context.ProjectProposals.Add(proposal);
            context.MatchRecords.Add(new MatchRecord
            {
                ProjectProposalId = proposal.Id,
                StudentId = student.Id,
                SupervisorId = supervisor.Id
            });
            await context.SaveChangesAsync();

            context.MatchRecords.Add(new MatchRecord
            {
                ProjectProposalId = proposal.Id,
                StudentId = student.Id,
                SupervisorId = supervisor.Id
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        [Fact]
        public async Task SavingDuplicateResearchAreaName_ThrowsDbUpdateException()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            context.ResearchAreas.Add(new ResearchArea
            {
                Name = "Digital Forensics",
                Description = "Forensics projects"
            });
            await context.SaveChangesAsync();

            context.ResearchAreas.Add(new ResearchArea
            {
                Name = "Digital Forensics",
                Description = "Duplicate"
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }
}
