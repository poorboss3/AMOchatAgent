using AMOchatAgent.Api.Models;

namespace AMOchatAgent.Api.Services;

public interface IAgentService
{
    Task<ChatResponse> ChatAsync(ChatRequest request);
    IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, CancellationToken cancellationToken = default);
    void ClearSession(string sessionId);
}
