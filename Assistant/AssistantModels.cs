using System.Text.Json;

namespace TaskAssistant.Mvc.Assistant;

/// <summary>One turn of prior conversation, kept only in-memory to support simple multi-turn follow-ups.</summary>
public record ConversationTurn(string Role, string Text);

/// <summary>
/// What the model decided to do, translated out of the Gemini-specific wire format.
/// Exactly one of (FunctionName+Arguments) or PlainTextReply is meaningful.
/// </summary>
public class AiDecision
{
    public bool IsFunctionCall { get; init; }
    public string? FunctionName { get; init; }
    public Dictionary<string, JsonElement> Arguments { get; init; } = new();
    public string? PlainTextReply { get; init; }

    public static AiDecision FromFunctionCall(string name, Dictionary<string, JsonElement> args) => new()
    {
        IsFunctionCall = true,
        FunctionName = name,
        Arguments = args,
    };

    public static AiDecision FromText(string? text) => new()
    {
        IsFunctionCall = false,
        PlainTextReply = text,
    };
}

/// <summary>Thrown when the AI provider cannot be reached or times out - never lets that crash the request.</summary>
public class AiUnavailableException : Exception
{
    public AiUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
