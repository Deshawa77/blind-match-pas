using BlindMatchPAS.Constants;
using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlindMatchPAS.Tests.Functional
{
    public class RoleWorkflowJourneyTests : IClassFixture<BlindMatchWebAppFactory>
    {
        private readonly BlindMatchWebAppFactory _factory;

        public RoleWorkflowJourneyTests(BlindMatchWebAppFactory factory)
        {
            _factory = factory;
            _factory.EnsureInitializedAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task StudentCanEditAndWithdrawProposal()
        {
            await FunctionalTestSupport.UpdateSettingsAsync(_factory, settings =>
            {
                settings.EmailNotificationsEnabled = false;
                settings.RequireConfirmedAccountToSignIn = false;
            });

            var email = $"student-edit-{Guid.NewGuid():N}@blindmatch.local";
            await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Editable Student", email, ApplicationRoles.Student, "Student12345!");
            var client = FunctionalTestSupport.CreateSecureClient(_factory);
            await FunctionalTestSupport.LoginAsync(client, email, "Student12345!");

            var createToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, "/Student/Create");
            await client.PostAsync(
                "/Student/Create",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = createToken,
                    ["OwnershipType"] = ProposalOwnershipTypes.Individual,
                    ["Title"] = "Editable Proposal",
                    ["Abstract"] = new string('E', 90),
                    ["TechnicalStack"] = "C#, ASP.NET Core",
                    ["ResearchAreaId"] = "1"
                }));

            int proposalId;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                proposalId = await context.ProjectProposals.Where(proposal => proposal.Title == "Editable Proposal").Select(proposal => proposal.Id).SingleAsync();
            }

            var editToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, $"/Student/Edit?id={proposalId}");
            var editResponse = await client.PostAsync(
                $"/Student/Edit?id={proposalId}",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = editToken,
                    ["Id"] = proposalId.ToString(),
                    ["OwnershipType"] = ProposalOwnershipTypes.Individual,
                    ["Title"] = "Updated Editable Proposal",
                    ["Abstract"] = new string('U', 95),
                    ["TechnicalStack"] = "C#, ASP.NET Core, SQL Server",
                    ["ResearchAreaId"] = "1"
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, editResponse.StatusCode);

            var withdrawToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, "/Student/MyProposals");
            var withdrawResponse = await client.PostAsync(
                $"/Student/Withdraw/{proposalId}",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = withdrawToken
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, withdrawResponse.StatusCode);

            var proposalsPage = await client.GetStringAsync("/Student/MyProposals");
            Assert.Contains("Updated Editable Proposal", proposalsPage);
            Assert.Contains("Withdrawn", proposalsPage);
        }

        [Fact]
        public async Task MatchedProposalCannotBeEdited()
        {
            var email = $"matched-student-{Guid.NewGuid():N}@blindmatch.local";
            var student = await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Matched Student", email, ApplicationRoles.Student, "Student12345!");

            int proposalId;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var proposal = new ProjectProposal
                {
                    Title = $"Locked Proposal {Guid.NewGuid():N}",
                    Abstract = new string('L', 90),
                    TechnicalStack = "ASP.NET Core, SQL Server",
                    ResearchAreaId = 1,
                    StudentId = student.Id,
                    Status = ProposalStatuses.Matched,
                    IsMatched = true,
                    IsIdentityRevealed = true
                };

                context.ProjectProposals.Add(proposal);
                await context.SaveChangesAsync();
                proposalId = proposal.Id;
            }

            var client = FunctionalTestSupport.CreateSecureClient(_factory);
            await FunctionalTestSupport.LoginAsync(client, email, "Student12345!");

            var editGetResponse = await client.GetAsync($"/Student/Edit?id={proposalId}");
            Assert.Equal(System.Net.HttpStatusCode.Redirect, editGetResponse.StatusCode);
            Assert.Equal("/Student/MyProposals", editGetResponse.Headers.Location?.OriginalString);

            var editToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, "/Student/MyProposals");
            var editPostResponse = await client.PostAsync(
                $"/Student/Edit?id={proposalId}",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = editToken,
                    ["Id"] = proposalId.ToString(),
                    ["OwnershipType"] = ProposalOwnershipTypes.Individual,
                    ["Title"] = "Attempted Update",
                    ["Abstract"] = new string('U', 90),
                    ["TechnicalStack"] = "Changed stack",
                    ["ResearchAreaId"] = "1"
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, editPostResponse.StatusCode);
            Assert.Equal("/Student/MyProposals", editPostResponse.Headers.Location?.OriginalString);

            using var verifyScope = _factory.Services.CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var proposalAfterAttempt = await verifyContext.ProjectProposals.SingleAsync(proposal => proposal.Id == proposalId);
            Assert.NotEqual("Attempted Update", proposalAfterAttempt.Title);
            Assert.Equal(ProposalStatuses.Matched, proposalAfterAttempt.Status);
        }

        [Fact]
        public async Task GroupLeadCanUpdateGroupBeforeProposalActivity()
        {
            await FunctionalTestSupport.UpdateSettingsAsync(_factory, settings =>
            {
                settings.EmailNotificationsEnabled = false;
            });

            var leadEmail = $"workflow-lead-{Guid.NewGuid():N}@blindmatch.local";
            var memberOne = await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Workflow Member One", $"workflow-member-one-{Guid.NewGuid():N}@blindmatch.local", ApplicationRoles.Student, "Student12345!");
            var memberTwo = await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Workflow Member Two", $"workflow-member-two-{Guid.NewGuid():N}@blindmatch.local", ApplicationRoles.Student, "Student12345!");
            await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Workflow Lead", leadEmail, ApplicationRoles.Student, "Student12345!");

            var client = FunctionalTestSupport.CreateSecureClient(_factory);
            await FunctionalTestSupport.LoginAsync(client, leadEmail, "Student12345!");

            var createGroupToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, "/Student/MyGroup");
            await client.PostAsync(
                "/Student/CreateGroup",
                FunctionalTestSupport.BuildFormContent(new List<KeyValuePair<string, string>>
                {
                    new("__RequestVerificationToken", createGroupToken),
                    new("Editor.GroupName", "Workflow Group"),
                    new("Editor.SelectedMemberIds", memberOne.Id)
                }));

            int groupId;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var leadId = await context.Users.Where(user => user.Email == leadEmail).Select(user => user.Id).SingleAsync();
                groupId = await context.ProjectGroups.Where(group => group.LeadStudentId == leadId).Select(group => group.Id).SingleAsync();
            }

            var updateToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, "/Student/MyGroup");
            var updateResponse = await client.PostAsync(
                "/Student/UpdateGroup",
                FunctionalTestSupport.BuildFormContent(new List<KeyValuePair<string, string>>
                {
                    new("__RequestVerificationToken", updateToken),
                    new("Editor.ProjectGroupId", groupId.ToString()),
                    new("Editor.GroupName", "Workflow Group Updated"),
                    new("Editor.SelectedMemberIds", memberOne.Id),
                    new("Editor.SelectedMemberIds", memberTwo.Id)
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, updateResponse.StatusCode);

            using var verifyScope = _factory.Services.CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var membershipCount = await verifyContext.ProjectGroupMembers.CountAsync(member => member.ProjectGroupId == groupId);
            Assert.Equal(3, membershipCount);
        }

        [Fact]
        public async Task SupervisorCanSetExpertiseAndLoadDashboard()
        {
            var email = $"dashboard-supervisor-{Guid.NewGuid():N}@blindmatch.local";
            await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Dashboard Supervisor", email, ApplicationRoles.Supervisor, "Supervisor12345!");

            var client = FunctionalTestSupport.CreateSecureClient(_factory);
            await FunctionalTestSupport.LoginAsync(client, email, "Supervisor12345!");

            var dashboardPage = await client.GetStringAsync("/Supervisor/Dashboard");
            Assert.Contains("Blind Review Dashboard", dashboardPage);

            var expertiseToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, "/Supervisor/SetExpertise");
            var expertiseResponse = await client.PostAsync(
                "/Supervisor/SetExpertise",
                FunctionalTestSupport.BuildFormContent(new List<KeyValuePair<string, string>>
                {
                    new("__RequestVerificationToken", expertiseToken),
                    new("SelectedResearchAreaIds", "1")
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, expertiseResponse.StatusCode);
            Assert.Equal("/Supervisor/BrowseProjects", expertiseResponse.Headers.Location?.OriginalString);

            var updatedDashboard = await client.GetStringAsync("/Supervisor/Dashboard");
            Assert.Contains("Artificial Intelligence", updatedDashboard);
        }

        [Fact]
        public async Task AdminDashboardAndAllocationsPagesRender()
        {
            var student = await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Allocation Student", $"allocation-student-{Guid.NewGuid():N}@blindmatch.local", ApplicationRoles.Student, "Student12345!");
            var supervisor = await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Allocation Supervisor", $"allocation-supervisor-{Guid.NewGuid():N}@blindmatch.local", ApplicationRoles.Supervisor, "Supervisor12345!");

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.SupervisorExpertise.Add(new SupervisorExpertise
                {
                    SupervisorId = supervisor.Id,
                    ResearchAreaId = 1
                });
                var proposal = new ProjectProposal
                {
                    Title = $"Allocation Proposal {Guid.NewGuid():N}",
                    Abstract = new string('A', 90),
                    TechnicalStack = "ASP.NET Core, SQL Server",
                    ResearchAreaId = 1,
                    StudentId = student.Id,
                    Status = ProposalStatuses.Matched,
                    IsMatched = true,
                    IsIdentityRevealed = true
                };
                context.ProjectProposals.Add(proposal);
                await context.SaveChangesAsync();

                context.MatchRecords.Add(new MatchRecord
                {
                    ProjectProposalId = proposal.Id,
                    StudentId = student.Id,
                    SupervisorId = supervisor.Id
                });
                await context.SaveChangesAsync();
            }

            var client = FunctionalTestSupport.CreateSecureClient(_factory);
            await FunctionalTestSupport.LoginAsync(client, "admin@blindmatch.local", "Admin12345!");

            var dashboardPage = await client.GetStringAsync("/Admin/Dashboard");
            Assert.Contains("Coordinator Dashboard", dashboardPage);

            var allocationsPage = await client.GetStringAsync("/Admin/Allocations");
            Assert.Contains("Allocations", allocationsPage);
        }
    }
}
