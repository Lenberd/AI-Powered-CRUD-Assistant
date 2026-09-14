namespace TaskAssistant.Mvc.Services.Exceptions;

/// <summary>The referenced task id (or title match) does not exist.</summary>
public class TaskNotFoundException : Exception
{
    public TaskNotFoundException(string message) : base(message) { }
}

/// <summary>A title-based reference (e.g. "the report task") matched more than one task.</summary>
public class AmbiguousTaskReferenceException : Exception
{
    public string Query { get; }
    public IReadOnlyList<Models.TaskItem> Candidates { get; }

    public AmbiguousTaskReferenceException(string query, IReadOnlyList<Models.TaskItem> candidates)
        : base($"\"{query}\" matches {candidates.Count} tasks.")
    {
        Query = query;
        Candidates = candidates;
    }
}

/// <summary>Arguments supplied for an operation (by a client or an AI-proposed call) are missing or invalid.</summary>
public class TaskValidationException : Exception
{
    public TaskValidationException(string message) : base(message) { }
}
