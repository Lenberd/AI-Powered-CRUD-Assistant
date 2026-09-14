using TaskAssistant.Mvc.Models;

namespace TaskAssistant.Mvc.Data;

/// <summary>
/// Pure ADO.NET data access for Tasks. This is the only layer allowed to touch SqlConnection/SqlCommand.
/// Every method is parameterized - callers (including AI-proposed calls) can never reach raw SQL.
/// </summary>
public interface ITaskRepository
{
    Task<IReadOnlyList<TaskItem>> GetAllAsync(bool? isDoneFilter = null, CancellationToken ct = default);

    Task<TaskItem?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Case-insensitive substring match against Title. Used to resolve natural-language references like "the report task".</summary>
    Task<IReadOnlyList<TaskItem>> FindByTitleAsync(string titleQuery, CancellationToken ct = default);

    Task<TaskItem> CreateAsync(TaskItem task, CancellationToken ct = default);

    /// <returns>true if a row was updated, false if the id did not exist.</returns>
    Task<bool> UpdateAsync(TaskItem task, CancellationToken ct = default);

    /// <returns>true if a row was deleted, false if the id did not exist.</returns>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
