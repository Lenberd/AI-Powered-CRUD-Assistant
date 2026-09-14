using TaskAssistant.Mvc.Models;

namespace TaskAssistant.Mvc.Services;

/// <summary>
/// Business/validation layer sitting on top of ITaskRepository. This is what both the plain REST
/// controllers and the AI assistant orchestrator call - neither talks to the database directly.
/// </summary>
public interface ITaskService
{
    Task<IReadOnlyList<TaskItem>> GetAllAsync(string? statusFilter = null, CancellationToken ct = default);

    /// <exception cref="Exceptions.TaskNotFoundException"/>
    Task<TaskItem> GetByIdAsync(int id, CancellationToken ct = default);

    /// <exception cref="Exceptions.TaskValidationException"/>
    Task<TaskItem> CreateAsync(string? title, bool isDone, DateTime? dueDate, CancellationToken ct = default);

    /// <exception cref="Exceptions.TaskNotFoundException"/>
    /// <exception cref="Exceptions.TaskValidationException"/>
    Task<TaskItem> UpdateAsync(int id, string? title, bool? isDone, DateTime? dueDate, bool clearDueDate, CancellationToken ct = default);

    /// <exception cref="Exceptions.TaskNotFoundException"/>
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Resolves a natural-language target (explicit id, or a title fragment like "the report task") to exactly one task.
    /// </summary>
    /// <exception cref="Exceptions.TaskNotFoundException"/>
    /// <exception cref="Exceptions.AmbiguousTaskReferenceException"/>
    /// <exception cref="Exceptions.TaskValidationException">Neither id nor titleQuery was supplied.</exception>
    Task<TaskItem> ResolveTaskAsync(int? id, string? titleQuery, CancellationToken ct = default);
}
