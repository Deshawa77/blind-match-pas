using BlindMatchPAS.Constants;
using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlindMatchPAS.Tests.Functional
{
    public class AdminJourneyTests : IClassFixture<BlindMatchWebAppFactory>
    {
        private readonly BlindMatchWebAppFactory _factory;

        public AdminJourneyTests(BlindMatchWebAppFactory factory)
        {
            _factory = factory;
            _factory.EnsureInitializedAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task AdminCanCreateEditAndDeleteUsers()
        {
            var client = FunctionalTestSupport.CreateSecureClient(_factory);
            var loginResponse = await FunctionalTestSupport.LoginAsync(client, "admin@blindmatch.local", "Admin12345!");
            Assert.Equal(System.Net.HttpStatusCode.Redirect, loginResponse.StatusCode);

            var userEmail = $"admin-managed-{Guid.NewGuid():N}@blindmatch.local";
            var createToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, "/Admin/CreateUser");
            var createResponse = await client.PostAsync(
                "/Admin/CreateUser",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = createToken,
                    ["FullName"] = "Admin Managed User",
                    ["Email"] = userEmail,
                    ["UserName"] = userEmail,
                    ["SelectedRole"] = ApplicationRoles.Student,
                    ["Password"] = "Student12345!",
                    ["ConfirmPassword"] = "Student12345!"
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, createResponse.StatusCode);
            Assert.Equal("/Admin/Users", createResponse.Headers.Location?.OriginalString);

            var userId = await FunctionalTestSupport.GetUserIdByEmailAsync(_factory, userEmail);
            var editToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, $"/Admin/EditUser?id={userId}");
            var editResponse = await client.PostAsync(
                "/Admin/EditUser",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = editToken,
                    ["Id"] = userId,
                    ["FullName"] = "Updated Managed User",
                    ["Email"] = userEmail,
                    ["UserName"] = userEmail,
                    ["SelectedRole"] = ApplicationRoles.Supervisor,
                    ["SupervisorCapacity"] = "3"
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, editResponse.StatusCode);

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var updatedUser = await context.Users.SingleAsync(user => user.Id == userId);
                Assert.Equal(ApplicationRoles.Supervisor, updatedUser.RoleType);
                Assert.Equal(3, updatedUser.SupervisorCapacity);
            }

            var deleteToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, "/Admin/Users");
            var deleteResponse = await client.PostAsync(
                $"/Admin/DeleteUser?id={userId}",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = deleteToken
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, deleteResponse.StatusCode);

            using var deleteScope = _factory.Services.CreateScope();
            var deleteContext = deleteScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await deleteContext.Users.AnyAsync(user => user.Id == userId));
        }

        [Fact]
        public async Task AdminCanManageResearchAreas()
        {
            var client = FunctionalTestSupport.CreateSecureClient(_factory);
            await FunctionalTestSupport.LoginAsync(client, "admin@blindmatch.local", "Admin12345!");

            var createToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, "/Admin/CreateResearchArea");
            var createResponse = await client.PostAsync(
                "/Admin/CreateResearchArea",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = createToken,
                    ["Name"] = $"Quantum Computing {Guid.NewGuid():N}",
                    ["Description"] = "Quantum projects"
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, createResponse.StatusCode);

            int researchAreaId;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                researchAreaId = await context.ResearchAreas
                    .OrderByDescending(area => area.Id)
                    .Select(area => area.Id)
                    .FirstAsync();
            }

            var editToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, $"/Admin/EditResearchArea?id={researchAreaId}");
            var editResponse = await client.PostAsync(
                "/Admin/EditResearchArea",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = editToken,
                    ["Id"] = researchAreaId.ToString(),
                    ["Name"] = "Quantum Software Engineering",
                    ["Description"] = "Updated taxonomy"
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, editResponse.StatusCode);

            var deleteToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, "/Admin/ResearchAreas");
            var deleteResponse = await client.PostAsync(
                $"/Admin/DeleteResearchArea?id={researchAreaId}",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = deleteToken
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, deleteResponse.StatusCode);

            using var deleteScope = _factory.Services.CreateScope();
            var deleteContext = deleteScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await deleteContext.ResearchAreas.AnyAsync(area => area.Id == researchAreaId));
        }

        [Fact]
        public async Task AdminCanReassignMatches()
        {
            var student = await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Reassign Student", $"reassign-student-{Guid.NewGuid():N}@blindmatch.local", ApplicationRoles.Student, "Student12345!");
            var oldSupervisor = await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Old Supervisor", $"old-supervisor-{Guid.NewGuid():N}@blindmatch.local", ApplicationRoles.Supervisor, "Supervisor12345!");
            var newSupervisor = await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "New Supervisor", $"new-supervisor-{Guid.NewGuid():N}@blindmatch.local", ApplicationRoles.Supervisor, "Supervisor12345!");

            int matchId;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var researchArea = new ResearchArea
                {
                    Name = $"Digital Twins {Guid.NewGuid():N}",
                    Description = "Aligned area"
                };

                context.ResearchAreas.Add(researchArea);
                await context.SaveChangesAsync();

                context.SupervisorExpertise.AddRange(
                    new SupervisorExpertise { SupervisorId = oldSupervisor.Id, ResearchAreaId = researchArea.Id },
                    new SupervisorExpertise { SupervisorId = newSupervisor.Id, ResearchAreaId = researchArea.Id });

                var proposal = new ProjectProposal
                {
                    Title = $"Reassignment Proposal {Guid.NewGuid():N}",
                    Abstract = new string('R', 90),
                    TechnicalStack = "ASP.NET Core, SQL Server",
                    ResearchAreaId = researchArea.Id,
                    StudentId = student.Id,
                    Status = ProposalStatuses.Matched,
                    IsMatched = true,
                    IsIdentityRevealed = true
                };

                context.ProjectProposals.Add(proposal);
                await context.SaveChangesAsync();

                var match = new MatchRecord
                {
                    ProjectProposalId = proposal.Id,
                    StudentId = student.Id,
                    SupervisorId = oldSupervisor.Id
                };

                context.MatchRecords.Add(match);
                await context.SaveChangesAsync();
                matchId = match.Id;
            }

            var client = FunctionalTestSupport.CreateSecureClient(_factory);
            await FunctionalTestSupport.LoginAsync(client, "admin@blindmatch.local", "Admin12345!");

            var token = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, $"/Admin/ReassignMatch?id={matchId}");
            var response = await client.PostAsync(
                "/Admin/ReassignMatch",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["MatchId"] = matchId.ToString(),
                    ["ProjectProposalId"] = "1",
                    ["CurrentSupervisorId"] = oldSupervisor.Id,
                    ["NewSupervisorId"] = newSupervisor.Id
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Admin/Allocations", response.Headers.Location?.OriginalString);

            using var verifyScope = _factory.Services.CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reassignedMatch = await verifyContext.MatchRecords.SingleAsync(match => match.Id == matchId);
            Assert.Equal(newSupervisor.Id, reassignedMatch.SupervisorId);
        }
    }
}
