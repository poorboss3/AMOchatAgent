using AMOchatAgent.Api.Models;
using AMOchatAgent.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace AMOchatAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IAgentService agentService, ILogger<ChatController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "sessionId and message are required" });

        _logger.LogInformation("Chat request: session={SessionId}, message={Message}", request.SessionId, request.Message);

        var response = await _agentService.ChatAsync(request);
        return Ok(response);
    }

    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = 400;
            return;
        }

        _logger.LogInformation("Stream chat request: session={SessionId}", request.SessionId);

        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var chunk in _agentService.StreamChatAsync(request, cancellationToken))
            {
                var data = $"data: {JsonSerializer.Serialize(new { type = "delta", content = chunk })}\n\n";
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(data), cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            var done = $"data: {JsonSerializer.Serialize(new { type = "done" })}\n\n";
            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(done), cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stream cancelled for session {SessionId}", request.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stream error for session {SessionId}", request.SessionId);
            var errorData = $"data: {JsonSerializer.Serialize(new { type = "error", content = "发生错误，请重试" })}\n\n";
            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(errorData));
            await Response.Body.FlushAsync();
        }
    }

    [HttpDelete("{sessionId}")]
    public IActionResult ClearSession(string sessionId)
    {
        _agentService.ClearSession(sessionId);
        _logger.LogInformation("Session cleared: {SessionId}", sessionId);
        return NoContent();
    }
}
