using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlindMatchPAS.Services
{
    internal static class DatabaseSchemaRecovery
    {
        private static readonly SemaphoreSlim RecoveryLock = new(1, 1);

        public static async Task<TResult> ExecuteWithMigrationRecoveryAsync<TResult>(
            DbContext context,
            ILogger logger,
            string missingObjectName,
            Func<Task<TResult>> operation)
        {
            try
            {
                return await operation();
            }
            catch (SqlException ex) when (IsMissingObject(ex, missingObjectName))
            {
                logger.LogWarning(
                    ex,
                    "Database object {MissingObjectName} was missing. Applying pending migrations and retrying.",
                    missingObjectName);

                await RecoverAsync(context, logger);
                return await operation();
            }
        }

        public static async Task ExecuteWithMigrationRecoveryAsync(
            DbContext context,
            ILogger logger,
            string missingObjectName,
            Func<Task> operation)
        {
            try
            {
                await operation();
            }
            catch (SqlException ex) when (IsMissingObject(ex, missingObjectName))
            {
                logger.LogWarning(
                    ex,
                    "Database object {MissingObjectName} was missing. Applying pending migrations and retrying.",
                    missingObjectName);

                await RecoverAsync(context, logger);
                await operation();
            }
        }

        private static bool IsMissingObject(SqlException exception, string missingObjectName)
        {
            return exception.Number == 208
                && exception.Message.Contains(missingObjectName, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task RecoverAsync(DbContext context, ILogger logger)
        {
            await RecoveryLock.WaitAsync();
            try
            {
                await context.Database.MigrateAsync();
                context.ChangeTracker.Clear();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Automatic database schema recovery failed.");
                throw;
            }
            finally
            {
                RecoveryLock.Release();
            }
        }
    }
}
