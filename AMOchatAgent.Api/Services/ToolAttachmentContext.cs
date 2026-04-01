using AMOchatAgent.Api.Models;

namespace AMOchatAgent.Api.Services;

/// <summary>
/// Scoped context: AgentService populates this before the tool-calling loop;
/// tools inject it to access the current request's attachments.
/// </summary>
public class ToolAttachmentContext
{
    public List<StoredAttachment> Attachments { get; } = new();
}
