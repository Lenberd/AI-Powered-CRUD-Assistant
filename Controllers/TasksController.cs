using Microsoft.AspNetCore.Mvc;
using TaskAssistant.Mvc.Models;
using TaskAssistant.Mvc.Models.Dtos;
using TaskAssistant.Mvc.Services;
using TaskAssistant.Mvc.Services.Exceptions;

namespace TaskAssistant.Mvc.Controllers;

/// <summary>
/// Conventional CRUD API, built and callable independently of the AI assistant.
/// This is what the assistant orchestrator ultimately drives under the hood.
/// </summary>
[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskItem>>> GetAll([FromQuery] string? status, CancellationToken ct)
    {
        try
        {
            return Ok(await _taskService.GetAllAsync(status, ct));
        }
        catch (TaskValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskItem>> GetById(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _taskService.GetByIdAsync(id, ct));
        }
        catch (TaskNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<TaskItem>> Create([FromBody] CreateTaskRequest request, CancellationToken ct)
    {
        try
        {
            var created = await _taskService.CreateAsync(request.Title, request.IsDone, request.DueDate, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (TaskValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TaskItem>> Update(int id, [FromBody] UpdateTaskRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await _taskService.UpdateAsync(id, request.Title, request.IsDone, request.DueDate, request.ClearDueDate, ct);
            return Ok(updated);
        }
        catch (TaskNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (TaskValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await _taskService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (TaskNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
