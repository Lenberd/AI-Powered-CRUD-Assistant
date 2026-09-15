using System.Text.Json;
using TaskAssistant.Mvc.Models;
using TaskAssistant.Mvc.Models.Dtos;
using TaskAssistant.Mvc.Services;
using TaskAssistant.Mvc.Services.Exceptions;

namespace TaskAssistant.Mvc.Assistant;

/// <summary>
/// The bridge described in the exam brief: takes whatever the AI proposed, validates it, and is the
/// ONLY thing that turns "AI decided to do X" into "code actually does X" via ITaskService. The AI
/// client above never sees ITaskRepository/ITaskService - this class is the sole caller of both.
/// </summary>
public class AssistantOrchestrator
{
    private readonly IAiAssistantClient _aiClient;
    private readonly ITaskService _taskService;
    private readonly ConversationStore _conversationStore;
    private readonly ILogger<AssistantOrchestrator> _logger;

    public AssistantOrchestrator(IAiAssistantClient aiClient, ITaskService taskService, ConversationStore conversationStore, ILogger<AssistantOrchestrator> logger)
    {
        _aiClient = aiClient;
        _taskService = taskService;
        _conversationStore = conversationStore;
        _logger = logger;
    }

    public async Task<AssistantResponse> HandleAsync(AssistantRequest request, CancellationToken ct)
    {
        // Bonus: explicit delete confirmation. The client re-submits with ConfirmDeleteId after the
        // first call came back "confirmation_required" - the AI is not consulted a second time.
        if (request.ConfirmDeleteId is int idToDelete)
        {
            return await ExecuteConfirmedDeleteAsync(idToDelete, ct);
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return new AssistantResponse { Status = "invalid_arguments", Result = "Please provide a message." };
        }

        var history = _conversationStore.GetHistory(request.ConversationId);
        var decision = await _aiClient.DecideActionAsync(request.Message, history, ct);

        // Bonus: audit log of every AI-proposed call, accepted or not - keeps the user's own message
        // next to the decision, so a reader can see "user asked X, AI proposed Y" in one line.
        _logger.LogInformation(
            "AI proposed {Function} for message \"{Message}\": args={Args}",
            decision.FunctionName ?? "(none - text reply)",
            request.Message,
            decision.IsFunctionCall ? JsonSerializer.Serialize(decision.Arguments) : decision.PlainTextReply);

        var response = decision.IsFunctionCall
            ? await ExecuteAsync(decision.FunctionName!, decision.Arguments, ct)
            : new AssistantResponse
            {
                Status = "rejected",
                Action = null,
                Result = decision.PlainTextReply?.Trim() is { Length: > 0 } text
                    ? text
                    : "I can only help with managing your tasks (creating, updating, listing, and deleting them).",
            };

        _conversationStore.Append(request.ConversationId, request.Message, $"[{response.Action ?? response.Status}] {response.Result}");
        return response;
    }

    private async Task<AssistantResponse> ExecuteAsync(string functionName, Dictionary<string, JsonElement> args, CancellationToken ct)
    {
        try
        {
            return functionName switch
            {
                AssistantToolCatalog.RejectRequest => HandleReject(args),
                AssistantToolCatalog.CreateTask => await HandleCreateAsync(args, ct),
                AssistantToolCatalog.UpdateTask => await HandleUpdateAsync(args, ct),
                AssistantToolCatalog.DeleteTask => await HandleDeleteProposalAsync(args, ct),
                AssistantToolCatalog.GetTask => await HandleGetAsync(args, ct),
                AssistantToolCatalog.ListTasks => await HandleListAsync(args, ct),
                _ => new AssistantResponse { Status = "error", Action = functionName, Result = $"The AI proposed an unknown action '{functionName}', which was rejected." },
            };
        }
        catch (TaskNotFoundException ex)
        {
            return new AssistantResponse { Status = "not_found", Action = functionName, Result = ex.Message };
        }
        catch (AmbiguousTaskReferenceException ex)
        {
            return new AssistantResponse { Status = "ambiguous", Action = functionName, Result = BuildAmbiguousMessage(functionName, ex), Candidates = ex.Candidates };
        }
        catch (TaskValidationException ex)
        {
            return new AssistantResponse { Status = "invalid_arguments", Action = functionName, Result = ex.Message };
        }
    }

    private static AssistantResponse HandleReject(Dictionary<string, JsonElement> args)
    {
        var reason = ArgReader.GetString(args, "reason");
        return new AssistantResponse
        {
            Status = "rejected",
            Action = AssistantToolCatalog.RejectRequest,
            Result = reason ?? "That's outside what this assistant can help with - I can only manage tasks.",
        };
    }

    private async Task<AssistantResponse> HandleCreateAsync(Dictionary<string, JsonElement> args, CancellationToken ct)
    {
        var title = ArgReader.GetString(args, "title");
        var isDone = ArgReader.GetBool(args, "isDone") ?? false;

        if (!ArgReader.TryGetDate(args, "dueDate", out var dueDate))
        {
            return Invalid(AssistantToolCatalog.CreateTask, "The AI proposed a due date that could not be understood.");
        }

        // TaskService.CreateAsync throws TaskValidationException for a missing/blank title - caught by ExecuteAsync.
        var created = await _taskService.CreateAsync(title, isDone, dueDate, ct);
        return new AssistantResponse
        {
            Status = "success",
            Action = AssistantToolCatalog.CreateTask,
            Result = $"Created task {created.Id}: \"{created.Title}\"" + (created.DueDate is { } d ? $" (due {d:yyyy-MM-dd})." : "."),
            Task = created,
        };
    }

    private async Task<AssistantResponse> HandleUpdateAsync(Dictionary<string, JsonElement> args, CancellationToken ct)
    {
        var id = ArgReader.GetInt(args, "id");
        var titleQuery = ArgReader.GetString(args, "titleQuery");
        var resolved = await _taskService.ResolveTaskAsync(id, titleQuery, ct);

        var newTitle = ArgReader.GetString(args, "title");
        var isDone = ArgReader.GetBool(args, "isDone");
        var clearDueDate = ArgReader.GetBool(args, "clearDueDate") ?? false;

        if (!ArgReader.TryGetDate(args, "dueDate", out var dueDate))
        {
            return Invalid(AssistantToolCatalog.UpdateTask, "The AI proposed a due date that could not be understood.");
        }

        var updated = await _taskService.UpdateAsync(resolved.Id, newTitle, isDone, dueDate, clearDueDate, ct);
        return new AssistantResponse
        {
            Status = "success",
            Action = AssistantToolCatalog.UpdateTask,
            Result = $"Updated task {updated.Id}: \"{updated.Title}\" (done: {updated.IsDone}{(updated.DueDate is { } d ? $", due {d:yyyy-MM-dd}" : "")}).",
            Task = updated,
        };
    }

    private async Task<AssistantResponse> HandleDeleteProposalAsync(Dictionary<string, JsonElement> args, CancellationToken ct)
    {
        var id = ArgReader.GetInt(args, "id");
        var titleQuery = ArgReader.GetString(args, "titleQuery");
        var resolved = await _taskService.ResolveTaskAsync(id, titleQuery, ct);

        // Bonus: destructive actions require an explicit confirmation round-trip before executing.
        return new AssistantResponse
        {
            Status = "confirmation_required",
            Action = AssistantToolCatalog.DeleteTask,
            Result = $"Delete task {resolved.Id} (\"{resolved.Title}\")? Confirm to proceed.",
            Task = resolved,
        };
    }

    private async Task<AssistantResponse> ExecuteConfirmedDeleteAsync(int id, CancellationToken ct)
    {
        try
        {
            var task = await _taskService.GetByIdAsync(id, ct);
            await _taskService.DeleteAsync(id, ct);
            _logger.LogInformation("Confirmed delete executed for task {Id}.", id);
            return new AssistantResponse
            {
                Status = "success",
                Action = AssistantToolCatalog.DeleteTask,
                Result = $"Deleted task {id}: \"{task.Title}\".",
            };
        }
        catch (TaskNotFoundException ex)
        {
            return new AssistantResponse { Status = "not_found", Action = AssistantToolCatalog.DeleteTask, Result = ex.Message };
        }
    }

    private async Task<AssistantResponse> HandleGetAsync(Dictionary<string, JsonElement> args, CancellationToken ct)
    {
        var id = ArgReader.GetInt(args, "id");
        var titleQuery = ArgReader.GetString(args, "titleQuery");
        var resolved = await _taskService.ResolveTaskAsync(id, titleQuery, ct);

        return new AssistantResponse
        {
            Status = "success",
            Action = AssistantToolCatalog.GetTask,
            Result = $"Task {resolved.Id}: \"{resolved.Title}\" (done: {resolved.IsDone}{(resolved.DueDate is { } d ? $", due {d:yyyy-MM-dd}" : "")}).",
            Task = resolved,
        };
    }

    private async Task<AssistantResponse> HandleListAsync(Dictionary<string, JsonElement> args, CancellationToken ct)
    {
        var status = ArgReader.GetString(args, "status") ?? "all";
        var tasks = await _taskService.GetAllAsync(status, ct);

        return new AssistantResponse
        {
            Status = "success",
            Action = AssistantToolCatalog.ListTasks,
            Result = tasks.Count == 0 ? "No matching tasks." : $"Found {tasks.Count} task(s).",
            Tasks = tasks,
        };
    }

    private static AssistantResponse Invalid(string action, string message) =>
        new() { Status = "invalid_arguments", Action = action, Result = message };

    private static string BuildAmbiguousMessage(string functionName, AmbiguousTaskReferenceException ex)
    {
        var verb = functionName switch
        {
            AssistantToolCatalog.DeleteTask => "delete",
            AssistantToolCatalog.UpdateTask => "update",
            AssistantToolCatalog.GetTask => "look up",
            _ => "match",
        };

        var numberedList = string.Join(
            "\n",
            ex.Candidates.Select((t, i) => $"{i + 1}. {t.Title} (task {t.Id})"));

        return $"I found multiple tasks matching \"{ex.Query}\":\n\n{numberedList}\n\nPlease specify which task you want to {verb} (e.g. by its id).";
    }
}
