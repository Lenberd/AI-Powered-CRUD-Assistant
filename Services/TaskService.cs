using TaskAssistant.Mvc.Data;
using TaskAssistant.Mvc.Models;
using TaskAssistant.Mvc.Services.Exceptions;

namespace TaskAssistant.Mvc.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _repository;

    public TaskService(ITaskRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(string? statusFilter = null, CancellationToken ct = default)
    {
        bool? isDoneFilter = statusFilter?.Trim().ToLowerInvariant() switch
        {
            null or "" or "all" => null,
            "done" or "complete" or "completed" => true,
            "pending" or "not-done" or "incomplete" or "undone" => false,
            _ => throw new TaskValidationException($"Unknown status filter '{statusFilter}'. Use 'all', 'done', or 'pending'."),
        };

        return await _repository.GetAllAsync(isDoneFilter, ct);
    }

    public async Task<TaskItem> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var task = await _repository.GetByIdAsync(id, ct);
        return task ?? throw new TaskNotFoundException($"Task {id} was not found.");
    }

    public async Task<TaskItem> CreateAsync(string? title, bool isDone, DateTime? dueDate, CancellationToken ct = default)
    {
        var trimmedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            throw new TaskValidationException("A non-empty title is required to create a task.");
        }

        var task = new TaskItem { Title = trimmedTitle, IsDone = isDone, DueDate = dueDate };
        return await _repository.CreateAsync(task, ct);
    }

    public async Task<TaskItem> UpdateAsync(int id, string? title, bool? isDone, DateTime? dueDate, bool clearDueDate, CancellationToken ct = default)
    {
        var existing = await GetByIdAsync(id, ct);

        if (title is not null)
        {
            var trimmedTitle = title.Trim();
            if (trimmedTitle.Length == 0)
            {
                throw new TaskValidationException("Title cannot be blank.");
            }
            existing.Title = trimmedTitle;
        }

        if (isDone.HasValue)
        {
            existing.IsDone = isDone.Value;
        }

        if (clearDueDate)
        {
            existing.DueDate = null;
        }
        else if (dueDate.HasValue)
        {
            existing.DueDate = dueDate.Value;
        }

        var updated = await _repository.UpdateAsync(existing, ct);
        if (!updated)
        {
            // Task existed a moment ago but was removed concurrently.
            throw new TaskNotFoundException($"Task {id} was not found.");
        }

        return existing;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var deleted = await _repository.DeleteAsync(id, ct);
        if (!deleted)
        {
            throw new TaskNotFoundException($"Task {id} was not found.");
        }
    }

    public async Task<TaskItem> ResolveTaskAsync(int? id, string? titleQuery, CancellationToken ct = default)
    {
        if (id.HasValue)
        {
            return await GetByIdAsync(id.Value, ct);
        }

        if (!string.IsNullOrWhiteSpace(titleQuery))
        {
            var matches = await _repository.FindByTitleAsync(titleQuery.Trim(), ct);
            return matches.Count switch
            {
                0 => throw new TaskNotFoundException($"No task matching \"{titleQuery}\" was found."),
                1 => matches[0],
                _ => throw new AmbiguousTaskReferenceException(titleQuery.Trim(), matches),
            };
        }

        throw new TaskValidationException("Either a task id or a title to search for is required.");
    }
}
