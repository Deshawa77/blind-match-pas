using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Tests.Helpers
{
    internal static class TestDbFactory
    {
        public static SqliteConnection CreateConnection()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            return connection;
        }

        public static ApplicationDbContext CreateContext(SqliteConnection connection)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        public static ApplicationUser CreateUser(string id, string fullName, string role)
        {
            return new ApplicationUser
            {
                Id = id,
                FullName = fullName,
                RoleType = role,
                UserName = $"{fullName.Replace(" ", string.Empty).ToLowerInvariant()}@blindmatch.local",
                Email = $"{id}@blindmatch.local",
                EmailConfirmed = true
            };
        }

        public static ResearchArea CreateResearchArea(int id, string name)
        {
            return new ResearchArea
            {
                Id = id,
                Name = name,
                Description = $"{name} projects"
            };
        }

        public static ProjectProposal CreateProposal(int id, int researchAreaId, string studentId)
        {
            return new ProjectProposal
            {
                Id = id,
                Title = $"Project {id}",
                Abstract = new string('A', 80),
                TechnicalStack = "ASP.NET Core, SQL Server",
                ResearchAreaId = researchAreaId,
                StudentId = studentId
            };
        }
    }
}
