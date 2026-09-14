using Microsoft.AspNetCore.Mvc;
using TaskAssistant.Mvc.Assistant;
using TaskAssistant.Mvc.Models.Dtos;

namespace TaskAssistant.Mvc.Controllers;

/// <summary>
/// The AI-driven endpoint. Accepts natural language, asks the AI layer which tool it wants to call,
/// and delegates execution to AssistantOrchestrator - this controller never touches the database.
/// </summary>
[ApiController]
[Route("api/assistant")]
public class AssistantController : ControllerBase
{
    private readonly AssistantOrchestrator _orchestrator;
    private readonly ILogger<AssistantController> _logger;

    public AssistantController(AssistantOrchestrator orchestrator, ILogger<AssistantController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<AssistantResponse>> Post([FromBody] AssistantRequest request, CancellationToken ct)
    {
        try
        {
            var response = await _orchestrator.HandleAsync(request, ct);
            return Ok(response);
        }
        catch (AiUnavailableException ex)
        {
            _logger.LogWarning(ex, "AI provider unavailable.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new AssistantResponse
            {
                Status = "error",
                Result = ex.Message,
            });
        }
        catch (Exception ex)
        {
            // Last-resort guard so a bad AI response or an unexpected downstream error never
            // crashes the endpoint - it always answers with a structured, explainable result.
            _logger.LogError(ex, "Unhandled error while processing assistant request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new AssistantResponse
            {
                Status = "error",
                Result = "Something went wrong processing that request. Please try again.",
            });
        }
    }
}
