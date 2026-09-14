using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TaskAssistant.Mvc.Configuration;

namespace TaskAssistant.Mvc.Assistant;

/// <summary>
/// Talks to Google Gemini's native function-calling API. This class ONLY ever returns a proposed
/// function name + arguments (or a plain-text non-call) - it has no reference to ITaskRepository
/// or any database type, so there is no code path by which the model's output can reach SQL directly.
/// </summary>
public class GeminiAiClient : IAiAssistantClient
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiAiClient> _logger;

    public GeminiAiClient(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiAiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiDecision> DecideActionAsync(string userMessage, IReadOnlyList<ConversationTurn> history, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new AiUnavailableException("The Gemini API key is not configured (Gemini:ApiKey). See README for setup.");
        }

        var request = new GeminiGenerateContentRequest
        {
            SystemInstruction = new GeminiContent
            {
                Role = "system",
                Parts = new List<GeminiPart> { new() { Text = BuildSystemPrompt() } },
            },
            Tools = new List<GeminiTool> { new() { FunctionDeclarations = AssistantToolCatalog.Declarations } },
            ToolConfig = new GeminiToolConfig { FunctionCallingConfig = new GeminiFunctionCallingConfig { Mode = "ANY" } },
        };

        foreach (var turn in history)
        {
            request.Contents.Add(new GeminiContent { Role = turn.Role, Parts = new List<GeminiPart> { new() { Text = turn.Text } } });
        }
        request.Contents.Add(new GeminiContent { Role = "user", Parts = new List<GeminiPart> { new() { Text = userMessage } } });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            var requestUri = $"{_options.BaseUrl.TrimEnd('/')}/models/{_options.Model}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";
            response = await _httpClient.PostAsJsonAsync(requestUri, request, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Gemini request timed out after {Timeout}s.", _options.TimeoutSeconds);
            throw new AiUnavailableException("The AI service timed out. Please try again in a moment.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Gemini request failed.");
            throw new AiUnavailableException("The AI service is currently unavailable. Please try again in a moment.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Gemini returned {Status}: {Body}", (int)response.StatusCode, body);
            throw new AiUnavailableException($"The AI service returned an error ({(int)response.StatusCode}).");
        }

        GeminiGenerateContentResponse? parsed;
        try
        {
            parsed = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(cancellationToken: ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse Gemini response.");
            throw new AiUnavailableException("The AI service returned an unexpected response.", ex);
        }

        if (parsed?.PromptFeedback?.BlockReason is { } blockReason)
        {
            return AiDecision.FromText($"The request was blocked by the AI safety filter ({blockReason}).");
        }

        var part = parsed?.Candidates?.FirstOrDefault()?.Content?.Parts?
            .FirstOrDefault(p => p.FunctionCall is not null);

        if (part?.FunctionCall is { } call)
        {
            return AiDecision.FromFunctionCall(call.Name, call.Args ?? new Dictionary<string, JsonElement>());
        }

        var text = parsed?.Candidates?.FirstOrDefault()?.Content?.Parts?
            .FirstOrDefault(p => p.Text is not null)?.Text;
        return AiDecision.FromText(text);
    }

    private static string BuildSystemPrompt() => $"""
        You are the backend decision-maker for a Task Manager app. Today's date is {DateTime.Now:yyyy-MM-dd} ({DateTime.Now:dddd}).

        Your ONLY job is to translate the user's message into exactly one function call from the
        provided tools. You never execute anything yourself and you never have database access -
        you only propose which function to call and with what arguments; a separate backend
        validates and executes it.

        Rules:
        - If the message asks to create, update, complete, delete, or list tasks, call the matching function.
        - If the user refers to a task by a numeric id (e.g. "task 3"), pass it as 'id'.
        - If the user refers to a task by name/description (e.g. "the report task", "delete finish report"),
          pass the identifying words as 'titleQuery' and leave 'id' unset. Never invent or guess a numeric id.
        - Resolve relative dates ("next Friday", "tomorrow", "in two days") into ISO-8601 using today's date above.
        - If the message is unrelated to task management (small talk, general knowledge, anything else),
          call reject_request - do not answer the question yourself and do not call any other function.
        - Always call exactly one function; never reply with plain text.
        """;
}
