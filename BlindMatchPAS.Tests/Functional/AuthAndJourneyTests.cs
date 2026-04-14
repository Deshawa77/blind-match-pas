using System.Net;
using System.Text.RegularExpressions;
using BlindMatchPAS.Constants;
using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BlindMatchPAS.Tests.Functional
{
    public class AuthAndJourneyTests : IClassFixture<BlindMatchWebAppFactory>
    {
        private readonly BlindMatchWebAppFactory _factory;

        public AuthAndJourneyTests(BlindMatchWebAppFactory factory)
        {
            _factory = factory;
            _factory.EnsureInitializedAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task StudentLoginAndProposalSubmission_RedirectsToDashboardAndPersistsProposal()
        {
            var email = $"functional-student-{Guid.NewGuid():N}@blindmatch.local";
            await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Functional Student", email, ApplicationRoles.Student, "Student12345!");

            var client = CreateSecureClient();
            var loginResponse = await FunctionalTestSupport.LoginAsync(client, email, "Student12345!");
            Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
            Assert.Equal("/Student/Dashboard", loginResponse.Headers.Location?.OriginalString);

            var createToken = await GetAntiforgeryTokenAsync(client, "/Student/Create");
            var proposalResponse = await client.PostAsync(
                "/Student/Create",
                BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = createToken,
                    ["Title"] = "Functional Project Submission",
                    ["Abstract"] = new string('A', 80),
                    ["TechnicalStack"] = "ASP.NET Core, SQL Server",
                    ["ResearchAreaId"] = "1"
                }));

            Assert.Equal(HttpStatusCode.Redirect, proposalResponse.StatusCode);
            Assert.Equal("/Student/MyProposals", proposalResponse.Headers.Location?.OriginalString);

            var myProposalsPage = await client.GetStringAsync("/Student/MyProposals");
            Assert.Contains("Functional Project Submission", myProposalsPage);
        }

        [Fact]
        public async Task SupervisorLoginAndInterestFlow_RedirectsToDashboardAndRecordsInterest()
        {
            var proposalId = 0;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var student = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    FullName = "Proposal Owner",
                    Email = $"proposal-owner-{Guid.NewGuid():N}@blindmatch.local",
                    UserName = $"proposal-owner-{Guid.NewGuid():N}@blindmatch.local",
                    EmailConfirmed = true,
                    RoleType = ApplicationRoles.Student
                };

                context.Users.Add(student);
                var proposal = new ProjectProposal
                {
                    Title = $"Supervisor Functional Proposal {Guid.NewGuid():N}",
                    Abstract = new string('B', 80),
                    TechnicalStack = "Python, FastAPI",
                    ResearchAreaId = 1,
                    StudentId = student.Id,
                    Status = ProposalStatuses.Pending
                };

                context.ProjectProposals.Add(proposal);
                await context.SaveChangesAsync();
                proposalId = proposal.Id;
            }

            var supervisorEmail = $"functional-supervisor-{Guid.NewGuid():N}@blindmatch.local";
            await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Functional Supervisor", supervisorEmail, ApplicationRoles.Supervisor, "Supervisor12345!");

            var client = CreateSecureClient();
            var loginResponse = await FunctionalTestSupport.LoginAsync(client, supervisorEmail, "Supervisor12345!");
            Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
            Assert.Equal("/Supervisor/Dashboard", loginResponse.Headers.Location?.OriginalString);

            var browsePage = await client.GetStringAsync("/Supervisor/BrowseProjects");
            Assert.Contains("Supervisor Functional Proposal", browsePage);

            var interestToken = await GetAntiforgeryTokenAsync(client, "/Supervisor/BrowseProjects");
            var expressInterestResponse = await client.PostAsync(
                $"/Supervisor/ExpressInterest/{proposalId}",
                BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = interestToken
                }));

            Assert.Equal(HttpStatusCode.Redirect, expressInterestResponse.StatusCode);

            var interestsPage = await client.GetStringAsync("/Supervisor/MyInterests");
            Assert.Contains("Supervisor Functional Proposal", interestsPage);
            Assert.Contains("Under Review", interestsPage);
        }

        [Fact]
        public async Task AdminLoginAndSettingsUpdate_RedirectsToDashboardAndSavesSettings()
        {
            var client = CreateSecureClient();
            var loginToken = await GetAntiforgeryTokenAsync(client, "/Identity/Account/Login");

            var loginResponse = await client.PostAsync(
                "/Identity/Account/Login",
                BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = loginToken,
                    ["Input.Identifier"] = "admin@blindmatch.local",
                    ["Input.Password"] = "Admin12345!",
                    ["Input.RememberMe"] = "false"
                }));

            Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
            Assert.Equal("/Admin/Dashboard", loginResponse.Headers.Location?.OriginalString);

            var settingsToken = await GetAntiforgeryTokenAsync(client, "/Admin/Settings");
            var updateResponse = await client.PostAsync(
                "/Admin/Settings",
                BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = settingsToken,
                    ["DefaultSupervisorCapacity"] = "5",
                    ["EmailNotificationsEnabled"] = "true",
                    ["AllowOptionalTwoFactor"] = "true"
                }));

            Assert.Equal(HttpStatusCode.Redirect, updateResponse.StatusCode);
            Assert.Equal("/Admin/Settings", updateResponse.Headers.Location?.OriginalString);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = await context.SystemSettings.SingleAsync();
            Assert.Equal(5, settings.DefaultSupervisorCapacity);
        }

        [Fact]
        public async Task ConfirmMatch_RevealsIdentityOnlyAfterConfirmationToBothStudentAndSupervisor()
        {
            var studentEmail = $"reveal-student-{Guid.NewGuid():N}@blindmatch.local";
            var supervisorEmail = $"reveal-supervisor-{Guid.NewGuid():N}@blindmatch.local";

            await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Reveal Student", studentEmail, ApplicationRoles.Student, "Student12345!");
            await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Reveal Supervisor", supervisorEmail, ApplicationRoles.Supervisor, "Supervisor12345!");

            var studentClient = CreateSecureClient();
            var studentLoginResponse = await FunctionalTestSupport.LoginAsync(studentClient, studentEmail, "Student12345!");
            Assert.Equal(HttpStatusCode.Redirect, studentLoginResponse.StatusCode);
            Assert.Equal("/Student/Dashboard", studentLoginResponse.Headers.Location?.OriginalString);

            var createToken = await GetAntiforgeryTokenAsync(studentClient, "/Student/Create");
            var proposalTitle = $"Identity Reveal Proposal {Guid.NewGuid():N}";
            var proposalResponse = await studentClient.PostAsync(
                "/Student/Create",
                BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = createToken,
                    ["Title"] = proposalTitle,
                    ["Abstract"] = new string('C', 90),
                    ["TechnicalStack"] = "ASP.NET Core, SQL Server",
                    ["ResearchAreaId"] = "1"
                }));

            Assert.Equal(HttpStatusCode.Redirect, proposalResponse.StatusCode);

            var studentProposalsBeforeMatch = await studentClient.GetStringAsync("/Student/MyProposals");
            Assert.Contains("Hidden until match confirmation.", studentProposalsBeforeMatch);

            var supervisorClient = CreateSecureClient();
            var supervisorLoginResponse = await FunctionalTestSupport.LoginAsync(supervisorClient, supervisorEmail, "Supervisor12345!");
            Assert.Equal(HttpStatusCode.Redirect, supervisorLoginResponse.StatusCode);
            Assert.Equal("/Supervisor/Dashboard", supervisorLoginResponse.Headers.Location?.OriginalString);

            var browsePage = await supervisorClient.GetStringAsync("/Supervisor/BrowseProjects");
            Assert.Contains(proposalTitle, browsePage);
            Assert.DoesNotContain("Reveal Student", browsePage);
            Assert.DoesNotContain(studentEmail, browsePage);

            var expressInterestToken = await GetAntiforgeryTokenAsync(supervisorClient, "/Supervisor/BrowseProjects");
            var proposalId = await GetProposalIdByTitleAsync(proposalTitle);
            var interestResponse = await supervisorClient.PostAsync(
                $"/Supervisor/ExpressInterest/{proposalId}",
                BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = expressInterestToken
                }));

            Assert.Equal(HttpStatusCode.Redirect, interestResponse.StatusCode);

            var interestId = await GetInterestIdAsync(proposalId, supervisorEmail);
            var confirmToken = await GetAntiforgeryTokenAsync(supervisorClient, "/Supervisor/MyInterests");
            var confirmResponse = await supervisorClient.PostAsync(
                $"/Supervisor/ConfirmMatch/{interestId}",
                BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = confirmToken
                }));

            Assert.Equal(HttpStatusCode.Redirect, confirmResponse.StatusCode);
            Assert.StartsWith("/Supervisor/RevealedMatch", confirmResponse.Headers.Location?.OriginalString, StringComparison.Ordinal);

            var revealedPage = await supervisorClient.GetStringAsync(confirmResponse.Headers.Location!.OriginalString!);
            Assert.Contains("Reveal Student", revealedPage);
            Assert.Contains(studentEmail, revealedPage);

            var studentProposalsAfterMatch = await studentClient.GetStringAsync("/Student/MyProposals");
            Assert.Contains("Reveal Supervisor", studentProposalsAfterMatch);
            Assert.Contains(supervisorEmail, studentProposalsAfterMatch);
        }

        [Fact]
        public async Task RbacBoundaries_BlockCrossRoleDashboardAccess()
        {
            var studentEmail = $"rbac-student-{Guid.NewGuid():N}@blindmatch.local";
            var supervisorEmail = $"rbac-supervisor-{Guid.NewGuid():N}@blindmatch.local";

            await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "RBAC Student", studentEmail, ApplicationRoles.Student, "Student12345!");
            await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "RBAC Supervisor", supervisorEmail, ApplicationRoles.Supervisor, "Supervisor12345!");

            var studentClient = CreateSecureClient();
            await FunctionalTestSupport.LoginAsync(studentClient, studentEmail, "Student12345!");

            var studentToSupervisorResponse = await studentClient.GetAsync("/Supervisor/Dashboard");
            Assert.Equal(HttpStatusCode.Redirect, studentToSupervisorResponse.StatusCode);
            Assert.Contains("/Identity/Account/AccessDenied", studentToSupervisorResponse.Headers.Location?.OriginalString);

            var supervisorClient = CreateSecureClient();
            await FunctionalTestSupport.LoginAsync(supervisorClient, supervisorEmail, "Supervisor12345!");

            var supervisorToAdminResponse = await supervisorClient.GetAsync("/Admin/Dashboard");
            Assert.Equal(HttpStatusCode.Redirect, supervisorToAdminResponse.StatusCode);
            Assert.Contains("/Identity/Account/AccessDenied", supervisorToAdminResponse.Headers.Location?.OriginalString);
        }

        private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
        {
            var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();

            var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
            Assert.True(match.Success, $"Antiforgery token was not found on page {path}.");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        private static FormUrlEncodedContent BuildFormContent(Dictionary<string, string> values)
        {
            return new FormUrlEncodedContent(values);
        }

        private async Task<int> GetProposalIdByTitleAsync(string title)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.ProjectProposals
                .Where(proposal => proposal.Title == title)
                .Select(proposal => proposal.Id)
                .SingleAsync();
        }

        private async Task<int> GetInterestIdAsync(int proposalId, string supervisorEmail)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.SupervisorInterests
                .Where(interest => interest.ProjectProposalId == proposalId)
                .Join(
                    context.Users,
                    interest => interest.SupervisorId,
                    user => user.Id,
                    (interest, user) => new { interest.Id, user.Email })
                .Where(item => item.Email == supervisorEmail)
                .Select(item => item.Id)
                .SingleAsync();
        }

        private HttpClient CreateSecureClient()
        {
            return _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
        }
    }
}
