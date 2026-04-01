namespace AMOchatAgent.Api.Models;

public class ChatRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<string>? AttachmentIds { get; set; }
}

public class ChatResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Reply { get; set; } = string.Empty;
    public string Status { get; set; } = "ongoing"; // ongoing | completed | error
    public object? OrderResult { get; set; }
}
