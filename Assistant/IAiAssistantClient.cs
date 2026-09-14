namespace TaskAssistant.Mvc.Assistant;

/// <summary>
/// The only thing this app's "AI layer" is allowed to produce: a proposed function name + arguments,
/// or a plain-text non-call. It never receives a database handle and never executes anything itself.
/// </summary>
public interface IAiAssistantClient
{
    Task<AiDecision> DecideActionAsync(string userMessage, IReadOnlyList<ConversationTurn> history, CancellationToken ct);
}
