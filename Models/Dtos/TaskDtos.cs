namespace TaskAssistant.Mvc.Models.Dtos;

// Payload for POST /api/tasks
public class CreateTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public DateTime? DueDate { get; set; }
}

// Payload for PUT /api/tasks/{id}. Fields are nullable so a client can send a partial update.
public class UpdateTaskRequest
{
    public string? Title { get; set; }
    public bool? IsDone { get; set; }
    public DateTime? DueDate { get; set; }
    public bool ClearDueDate { get; set; }
}
