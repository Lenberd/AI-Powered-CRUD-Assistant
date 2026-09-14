using System.Text.Json.Serialization;

namespace TaskAssistant.Mvc.Assistant;

// ---- Request models (Gemini generateContent, v1beta) ----

public class GeminiSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "OBJECT";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, GeminiSchema>? Properties { get; set; }

    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }

    [JsonPropertyName("enum")]
    public List<string>? Enum { get; set; }
}

public class GeminiFunctionDeclaration
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public GeminiSchema Parameters { get; set; } = new();
}

public class GeminiTool
{
    [JsonPropertyName("functionDeclarations")]
    public IReadOnlyList<GeminiFunctionDeclaration> FunctionDeclarations { get; set; } = new List<GeminiFunctionDeclaration>();
}

public class GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("functionCall")]
    public GeminiFunctionCall? FunctionCall { get; set; }
}

public class GeminiFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public Dictionary<string, System.Text.Json.JsonElement>? Args { get; set; }
}

public class GeminiContent
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = new();
}

public class GeminiFunctionCallingConfig
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "ANY";
}

public class GeminiToolConfig
{
    [JsonPropertyName("functionCallingConfig")]
    public GeminiFunctionCallingConfig FunctionCallingConfig { get; set; } = new();
}

public class GeminiGenerateContentRequest
{
    [JsonPropertyName("systemInstruction")]
    public GeminiContent? SystemInstruction { get; set; }

    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<GeminiTool> Tools { get; set; } = new();

    [JsonPropertyName("toolConfig")]
    public GeminiToolConfig? ToolConfig { get; set; }
}

// ---- Response models ----

public class GeminiGenerateContentResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }

    [JsonPropertyName("promptFeedback")]
    public GeminiPromptFeedback? PromptFeedback { get; set; }
}

public class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }
}

public class GeminiPromptFeedback
{
    [JsonPropertyName("blockReason")]
    public string? BlockReason { get; set; }
}
