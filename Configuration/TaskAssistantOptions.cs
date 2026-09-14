namespace TaskAssistant.Mvc.Configuration;

public class TaskAssistantOptions
{
    public string? ConnectionString { get; set; }
}

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.6-flash";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
    public int TimeoutSeconds { get; set; } = 30;
}
