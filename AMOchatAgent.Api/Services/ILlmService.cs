using AMOchatAgent.Api.Models;

namespace AMOchatAgent.Api.Services;

public interface ILlmService
{
    Task<LlmMessage> ChatAsync(List<LlmMessage> messages, List<ToolDefinition>? tools = null);
    IAsyncEnumerable<string> StreamChatAsync(List<LlmMessage> messages, List<ToolDefinition>? tools = null, CancellationToken cancellationToken = default);
}
