using BlindMatchPAS.Constants;
using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlindMatchPAS.Tests.Functional
{
    public class GroupAndAnonymityJourneyTests : IClassFixture<BlindMatchWebAppFactory>
    {
        private readonly BlindMatchWebAppFactory _factory;

        public GroupAndAnonymityJourneyTests(BlindMatchWebAppFactory factory)
        {
            _factory = factory;
            _factory.EnsureInitializedAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task GroupLeadWorkflow_AllowsMemberToSeeRevealedSupervisorDetails()
        {
            await FunctionalTestSupport.UpdateSettingsAsync(_factory, settings =>
            {
                settings.EmailNotificationsEnabled = false;
                settings.RequireConfirmedAccountToSignIn = false;
                settings.AllowOptionalTwoFactor = true;
            });

            var memberEmail = $"group-member-{Guid.NewGuid():N}@blindmatch.local";
            var member = await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Group Member", memberEmail, ApplicationRoles.Student, "Student12345!");

            var supervisorEmail = $"group-supervisor-{Guid.NewGuid():N}@blindmatch.local";
            var supervisor = await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Group Supervisor", supervisorEmail, ApplicationRoles.Supervisor, "Supervisor12345!");

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.SupervisorExpertise.Add(new SupervisorExpertise
                {
                    SupervisorId = supervisor.Id,
                    ResearchAreaId = 1
                });
                await context.SaveChangesAsync();
            }

            var leadEmail = $"group-lead-{Guid.NewGuid():N}@blindmatch.local";
            await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Group Lead", leadEmail, ApplicationRoles.Student, "Student12345!");
            var leadClient = FunctionalTestSupport.CreateSecureClient(_factory);
            await FunctionalTestSupport.LoginAsync(leadClient, leadEmail, "Student12345!");

            var createGroupToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(leadClient, "/Student/MyGroup");
            var createGroupResponse = await leadClient.PostAsync(
                "/Student/CreateGroup",
                FunctionalTestSupport.BuildFormContent(new List<KeyValuePair<string, string>>
                {
                    new("__RequestVerificationToken", createGroupToken),
                    new("Editor.GroupName", "Visionary Builders"),
                    new("Editor.SelectedMemberIds", member.Id)
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, createGroupResponse.StatusCode);
            Assert.Equal("/Student/MyGroup", createGroupResponse.Headers.Location?.OriginalString);

            int groupId;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var leadStudentId = await context.Users
                    .Where(user => user.Email == leadEmail)
                    .Select(user => user.Id)
                    .SingleAsync();
                groupId = await context.ProjectGroups
                    .Where(group => group.LeadStudentId == leadStudentId)
                    .Select(group => group.Id)
                    .SingleAsync();
            }

            var proposalTitle = $"Group Proposal {Guid.NewGuid():N}";
            var createProposalToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(leadClient, "/Student/Create");
            var createProposalResponse = await leadClient.PostAsync(
                "/Student/Create",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = createProposalToken,
                    ["OwnershipType"] = ProposalOwnershipTypes.Group,
                    ["ProjectGroupId"] = groupId.ToString(),
                    ["Title"] = proposalTitle,
                    ["Abstract"] = new string('P', 100),
                    ["TechnicalStack"] = "ASP.NET Core, SQL Server, xUnit",
                    ["ResearchAreaId"] = "1"
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, createProposalResponse.StatusCode);
            Assert.Equal("/Student/MyProposals", createProposalResponse.Headers.Location?.OriginalString);

            var supervisorClient = FunctionalTestSupport.CreateSecureClient(_factory);
            var supervisorLogin = await FunctionalTestSupport.LoginAsync(supervisorClient, supervisorEmail, "Supervisor12345!");
            Assert.Equal(System.Net.HttpStatusCode.Redirect, supervisorLogin.StatusCode);

            var browsePage = await supervisorClient.GetStringAsync("/Supervisor/BrowseProjects");
            Assert.Contains(proposalTitle, browsePage);
            Assert.DoesNotContain("Group Lead", browsePage);
            Assert.DoesNotContain(memberEmail, browsePage);
            Assert.DoesNotContain("Visionary Builders", browsePage);

            int proposalId;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                proposalId = await context.ProjectProposals.Where(proposal => proposal.Title == proposalTitle).Select(proposal => proposal.Id).SingleAsync();
            }

            var interestToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(supervisorClient, "/Supervisor/BrowseProjects");
            var expressInterestResponse = await supervisorClient.PostAsync(
                $"/Supervisor/ExpressInterest/{proposalId}",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = interestToken
                }));
            Assert.Equal(System.Net.HttpStatusCode.Redirect, expressInterestResponse.StatusCode);

            int interestId;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                interestId = await context.SupervisorInterests.Where(interest => interest.ProjectProposalId == proposalId).Select(interest => interest.Id).SingleAsync();
            }

            var confirmToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(supervisorClient, "/Supervisor/MyInterests");
            var confirmResponse = await supervisorClient.PostAsync(
                $"/Supervisor/ConfirmMatch/{interestId}",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = confirmToken
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, confirmResponse.StatusCode);
            Assert.Contains("/Supervisor/RevealedMatch", confirmResponse.Headers.Location?.OriginalString);

            var memberClient = FunctionalTestSupport.CreateSecureClient(_factory);
            var memberLogin = await FunctionalTestSupport.LoginAsync(memberClient, memberEmail, "Student12345!");
            Assert.Equal(System.Net.HttpStatusCode.Redirect, memberLogin.StatusCode);

            var memberProposalsPage = await memberClient.GetStringAsync("/Student/MyProposals");
            Assert.Contains(proposalTitle, memberProposalsPage);
            Assert.Contains("Group Supervisor", memberProposalsPage);
            Assert.Contains(supervisorEmail, memberProposalsPage);
        }

        [Fact]
        public async Task SupervisorBrowseProjects_HidesStudentIdentityAndGroupOwnership()
        {
            await FunctionalTestSupport.UpdateSettingsAsync(_factory, settings =>
            {
                settings.EmailNotificationsEnabled = false;
                settings.RequireConfirmedAccountToSignIn = false;
            });

            var lead = await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Anonymous Lead", $"anonymous-lead-{Guid.NewGuid():N}@blindmatch.local", ApplicationRoles.Student, "Student12345!");
            var member = await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Anonymous Member", $"anonymous-member-{Guid.NewGuid():N}@blindmatch.local", ApplicationRoles.Student, "Student12345!");
            var supervisorEmail = $"anonymous-supervisor-{Guid.NewGuid():N}@blindmatch.local";
            var supervisor = await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Anonymous Supervisor", supervisorEmail, ApplicationRoles.Supervisor, "Supervisor12345!");

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var group = new ProjectGroup
                {
                    Name = "Hidden Innovators",
                    LeadStudentId = lead.Id,
                    Members = new List<ProjectGroupMember>
                    {
                        new() { StudentId = lead.Id, IsLead = true },
                        new() { StudentId = member.Id, IsLead = false }
                    }
                };

                context.ProjectGroups.Add(group);
                await context.SaveChangesAsync();

                context.ProjectProposals.Add(new ProjectProposal
                {
                    Title = $"Anonymous Queue Proposal {Guid.NewGuid():N}",
                    Abstract = new string('A', 90),
                    TechnicalStack = "React, ASP.NET Core",
                    ResearchAreaId = 1,
                    StudentId = lead.Id,
                    ProjectGroupId = group.Id,
                    Status = ProposalStatuses.Pending
                });

                context.SupervisorExpertise.Add(new SupervisorExpertise
                {
                    SupervisorId = supervisor.Id,
                    ResearchAreaId = 1
                });
                await context.SaveChangesAsync();
            }

            var supervisorClient = FunctionalTestSupport.CreateSecureClient(_factory);
            var loginResponse = await FunctionalTestSupport.LoginAsync(supervisorClient, supervisorEmail, "Supervisor12345!");
            Assert.Equal(System.Net.HttpStatusCode.Redirect, loginResponse.StatusCode);

            var browsePage = await supervisorClient.GetStringAsync("/Supervisor/BrowseProjects");
            Assert.DoesNotContain("Anonymous Lead", browsePage);
            Assert.DoesNotContain("Anonymous Member", browsePage);
            Assert.DoesNotContain("Hidden Innovators", browsePage);
            Assert.DoesNotContain(lead.Email ?? string.Empty, browsePage);
            Assert.DoesNotContain(member.Email ?? string.Empty, browsePage);
        }
    }
}
