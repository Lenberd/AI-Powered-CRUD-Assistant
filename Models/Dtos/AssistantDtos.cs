using TaskAssistant.Mvc.Models;

namespace TaskAssistant.Mvc.Models.Dtos;

public class AssistantRequest
{
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional client-generated id used to thread multi-turn follow-ups ("actually make that due tomorrow").</summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// Set by the client on a second call to actually execute a delete that was previously
    /// reported back as "confirmation_required" with this same task id. The AI is not consulted again.
    /// </summary>
    public int? ConfirmDeleteId { get; set; }
}

public class AssistantResponse
{
    /// <summary>Machine-readable outcome: success | rejected | not_found | ambiguous | invalid_arguments | confirmation_required | error.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Which tool the AI proposed (e.g. "update_task"), if any.</summary>
    public string? Action { get; set; }

    /// <summary>Human-readable summary of what happened, per the exam's example response shape.</summary>
    public string Result { get; set; } = string.Empty;

    public TaskItem? Task { get; set; }
    public IReadOnlyList<TaskItem>? Tasks { get; set; }
    public IReadOnlyList<TaskItem>? Candidates { get; set; }
}
