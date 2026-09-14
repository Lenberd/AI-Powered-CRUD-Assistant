using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TaskAssistant.Mvc.Configuration;
using TaskAssistant.Mvc.Models;

namespace TaskAssistant.Mvc.Data;

/// <summary>
/// ADO.NET implementation of ITaskRepository against SQL Server / LocalDB.
/// Every command is parameterized (SqlParameter) - no string concatenation of SQL or values, ever.
/// Connections and commands are always opened/disposed via "using".
/// </summary>
public class SqlTaskRepository : ITaskRepository
{
    private readonly string _connectionString;

    public SqlTaskRepository(IOptions<TaskAssistantOptions> options)
    {
        _connectionString = options.Value.ConnectionString
            ?? throw new InvalidOperationException("ConnectionStrings:TaskAssistantDb is not configured.");
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(bool? isDoneFilter = null, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, Title, IsDone, DueDate
            FROM dbo.Tasks
            WHERE (@IsDoneFilter IS NULL OR IsDone = @IsDoneFilter)
            ORDER BY Id;
            """;

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@IsDoneFilter", System.Data.SqlDbType.Bit).Value = (object?)isDoneFilter ?? DBNull.Value;

        await connection.OpenAsync(ct);
        using var reader = await command.ExecuteReaderAsync(ct);

        var results = new List<TaskItem>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(Map(reader));
        }
        return results;
    }

    public async Task<TaskItem?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = "SELECT Id, Title, IsDone, DueDate FROM dbo.Tasks WHERE Id = @Id;";

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = id;

        await connection.OpenAsync(ct);
        using var reader = await command.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<TaskItem>> FindByTitleAsync(string titleQuery, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, Title, IsDone, DueDate
            FROM dbo.Tasks
            WHERE Title LIKE '%' + @TitleQuery + '%'
            ORDER BY Id;
            """;

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        // Parameterized even though it feeds a LIKE clause - SQL Server treats the parameter value
        // as pure data, so wildcard/quote/keyword characters inside it cannot break out of the query.
        command.Parameters.Add("@TitleQuery", System.Data.SqlDbType.NVarChar, 200).Value = titleQuery;

        await connection.OpenAsync(ct);
        using var reader = await command.ExecuteReaderAsync(ct);

        var results = new List<TaskItem>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(Map(reader));
        }
        return results;
    }

    public async Task<TaskItem> CreateAsync(TaskItem task, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.Tasks (Title, IsDone, DueDate)
            OUTPUT INSERTED.Id
            VALUES (@Title, @IsDone, @DueDate);
            """;

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Title", System.Data.SqlDbType.NVarChar, 200).Value = task.Title;
        command.Parameters.Add("@IsDone", System.Data.SqlDbType.Bit).Value = task.IsDone;
        command.Parameters.Add("@DueDate", System.Data.SqlDbType.DateTime2).Value = (object?)task.DueDate ?? DBNull.Value;

        await connection.OpenAsync(ct);
        var newId = (int)await command.ExecuteScalarAsync(ct)!;

        task.Id = newId;
        return task;
    }

    public async Task<bool> UpdateAsync(TaskItem task, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.Tasks
            SET Title = @Title, IsDone = @IsDone, DueDate = @DueDate
            WHERE Id = @Id;
            """;

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = task.Id;
        command.Parameters.Add("@Title", System.Data.SqlDbType.NVarChar, 200).Value = task.Title;
        command.Parameters.Add("@IsDone", System.Data.SqlDbType.Bit).Value = task.IsDone;
        command.Parameters.Add("@DueDate", System.Data.SqlDbType.DateTime2).Value = (object?)task.DueDate ?? DBNull.Value;

        await connection.OpenAsync(ct);
        var rowsAffected = await command.ExecuteNonQueryAsync(ct);
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM dbo.Tasks WHERE Id = @Id;";

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = id;

        await connection.OpenAsync(ct);
        var rowsAffected = await command.ExecuteNonQueryAsync(ct);
        return rowsAffected > 0;
    }

    private static TaskItem Map(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Title = reader.GetString(1),
        IsDone = reader.GetBoolean(2),
        DueDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
    };
}
