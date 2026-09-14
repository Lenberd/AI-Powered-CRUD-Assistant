namespace TaskAssistant.Mvc.Assistant;

/// <summary>
/// The single source of truth for what the AI is allowed to propose. Each entry becomes a Gemini
/// "function declaration" (native structured tool-calling - the model returns a typed function
/// name + args, never free-form SQL or JSON-in-text). The orchestrator switches on these same
/// names when deciding what to actually execute against the database.
/// </summary>
public static class AssistantToolCatalog
{
    public const string CreateTask = "create_task";
    public const string UpdateTask = "update_task";
    public const string DeleteTask = "delete_task";
    public const string GetTask = "get_task";
    public const string ListTasks = "list_tasks";
    public const string RejectRequest = "reject_request";

    public static readonly IReadOnlyList<GeminiFunctionDeclaration> Declarations = new List<GeminiFunctionDeclaration>
    {
        new()
        {
            Name = CreateTask,
            Description = "Create a brand new task. Use this when the user asks to add/create a new task.",
            Parameters = new GeminiSchema
            {
                Type = "OBJECT",
                Properties = new Dictionary<string, GeminiSchema>
                {
                    ["title"] = new() { Type = "STRING", Description = "The task title, exactly as the user described it (without filler words like 'called' or 'a task to')." },
                    ["dueDate"] = new() { Type = "STRING", Description = "Optional due date/time resolved to strict ISO-8601 (YYYY-MM-DD or YYYY-MM-DDTHH:mm:ss). Resolve relative phrases like 'next Friday' or 'tomorrow' using the current date given in the system instructions. Omit if no due date was mentioned." },
                    ["isDone"] = new() { Type = "BOOLEAN", Description = "Optional. Only true if the user explicitly says the task is already done. Defaults to false." },
                },
                Required = new List<string> { "title" },
            },
        },
        new()
        {
            Name = UpdateTask,
            Description = "Change an existing task: mark done/not done, rename it, or change its due date. Provide 'id' if the user gave a numeric id, otherwise provide 'titleQuery' with the words identifying the task by title.",
            Parameters = new GeminiSchema
            {
                Type = "OBJECT",
                Properties = new Dictionary<string, GeminiSchema>
                {
                    ["id"] = new() { Type = "INTEGER", Description = "The numeric task id, if the user gave one explicitly (e.g. 'task 3')." },
                    ["titleQuery"] = new() { Type = "STRING", Description = "A fragment of the task's title to look it up by, if no numeric id was given (e.g. 'report' for 'the report task')." },
                    ["title"] = new() { Type = "STRING", Description = "New title, only if the user wants to rename the task." },
                    ["isDone"] = new() { Type = "BOOLEAN", Description = "New completion state, only if the user wants to mark it done or not done." },
                    ["dueDate"] = new() { Type = "STRING", Description = "New due date in ISO-8601, only if the user wants to change it." },
                    ["clearDueDate"] = new() { Type = "BOOLEAN", Description = "Set true only if the user explicitly wants to remove/clear the due date." },
                },
                Required = new List<string>(),
            },
        },
        new()
        {
            Name = DeleteTask,
            Description = "Permanently delete a task. Provide 'id' if the user gave a numeric id, otherwise provide 'titleQuery' with the words identifying the task by title.",
            Parameters = new GeminiSchema
            {
                Type = "OBJECT",
                Properties = new Dictionary<string, GeminiSchema>
                {
                    ["id"] = new() { Type = "INTEGER", Description = "The numeric task id, if given explicitly." },
                    ["titleQuery"] = new() { Type = "STRING", Description = "A fragment of the task's title to look it up by, if no numeric id was given." },
                },
                Required = new List<string>(),
            },
        },
        new()
        {
            Name = GetTask,
            Description = "Look up and return a single task's details. Provide 'id' if the user gave a numeric id, otherwise provide 'titleQuery'.",
            Parameters = new GeminiSchema
            {
                Type = "OBJECT",
                Properties = new Dictionary<string, GeminiSchema>
                {
                    ["id"] = new() { Type = "INTEGER", Description = "The numeric task id, if given explicitly." },
                    ["titleQuery"] = new() { Type = "STRING", Description = "A fragment of the task's title to look it up by, if no numeric id was given." },
                },
                Required = new List<string>(),
            },
        },
        new()
        {
            Name = ListTasks,
            Description = "List multiple tasks, optionally filtered by completion status. Use this for requests like 'show me everything', 'what's not done yet', or 'list completed tasks'.",
            Parameters = new GeminiSchema
            {
                Type = "OBJECT",
                Properties = new Dictionary<string, GeminiSchema>
                {
                    ["status"] = new()
                    {
                        Type = "STRING",
                        Description = "Which tasks to include.",
                        Enum = new List<string> { "all", "done", "pending" },
                    },
                },
                Required = new List<string>(),
            },
        },
        new()
        {
            Name = RejectRequest,
            Description = "Call this for ANY message that is not a request to create, update, delete, complete, or list tasks - e.g. small talk, general knowledge questions, or anything unrelated to task management. Do not attempt to answer such questions yourself.",
            Parameters = new GeminiSchema
            {
                Type = "OBJECT",
                Properties = new Dictionary<string, GeminiSchema>
                {
                    ["reason"] = new() { Type = "STRING", Description = "A short, polite one-sentence explanation of why the request is out of scope for a task manager assistant." },
                },
                Required = new List<string> { "reason" },
            },
        },
    };
}
