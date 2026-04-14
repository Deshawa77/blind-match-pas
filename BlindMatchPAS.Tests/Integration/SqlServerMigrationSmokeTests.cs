using BlindMatchPAS.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace BlindMatchPAS.Tests.Integration
{
    public class SqlServerMigrationSmokeTests
    {
        [Fact]
        public async Task DatabaseMigrateAsync_AppliesSchemaOnFreshLocalDb()
        {
            var databaseName = $"BlindMatchPAS_MigrationSmoke_{Guid.NewGuid():N}";
            var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False";

            await using var context = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlServer(connectionString)
                    .Options);

            try
            {
                await context.Database.MigrateAsync();
                var pending = await context.Database.GetPendingMigrationsAsync();
                Assert.Empty(pending);

                var connection = context.Database.GetDbConnection();
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM [ProjectGroups]";
                var result = await command.ExecuteScalarAsync();
                Assert.NotNull(result);
            }
            catch (SqlException ex) when (IsLocalDbUnavailable(ex))
            {
                return;
            }
            finally
            {
                try
                {
                    await context.Database.EnsureDeletedAsync();
                }
                catch
                {
                    // Ignore cleanup failures in smoke-test teardown.
                }
            }
        }

        private static bool IsLocalDbUnavailable(SqlException exception)
        {
            return exception.Message.Contains("LocalDB", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("server was not found", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("network-related", StringComparison.OrdinalIgnoreCase);
        }
    }
}
