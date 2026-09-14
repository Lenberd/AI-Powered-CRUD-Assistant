using Microsoft.Data.SqlClient;

namespace TaskAssistant.Mvc.Data;

/// <summary>
/// Creates the Tasks table on startup if it doesn't already exist, so the exam runs against a
/// fresh LocalDB/SQL Server instance with zero manual setup beyond a connection string.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(string connectionString, ILogger logger)
    {
        const string createTableSql = """
            IF OBJECT_ID('dbo.Tasks', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Tasks (
                    Id        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    Title     NVARCHAR(200)      NOT NULL,
                    IsDone    BIT                NOT NULL DEFAULT (0),
                    DueDate   DATETIME2          NULL
                );
            END
            """;

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;

            if (!string.IsNullOrEmpty(databaseName))
            {
                // Connect to "master" first to create the database itself if it doesn't exist yet -
                // a fresh LocalDB instance has no user databases at all.
                var masterBuilder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
                using var masterConnection = new SqlConnection(masterBuilder.ConnectionString);
                await masterConnection.OpenAsync();

                var createDbSql = $"IF DB_ID(@DbName) IS NULL EXEC('CREATE DATABASE [{databaseName.Replace("]", "]]")}]');";
                using var createDbCommand = new SqlCommand(createDbSql, masterConnection);
                createDbCommand.Parameters.Add("@DbName", System.Data.SqlDbType.NVarChar).Value = databaseName;
                await createDbCommand.ExecuteNonQueryAsync();
            }

            using var connection = new SqlConnection(connectionString);
            using var command = new SqlCommand(createTableSql, connection);
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
            logger.LogInformation("Database ready: dbo.Tasks verified/created.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not initialize the database. Is SQL Server/LocalDB running and is the connection string correct?");
            throw;
        }
    }
}
