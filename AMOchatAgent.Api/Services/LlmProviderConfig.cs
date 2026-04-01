namespace AMOchatAgent.Api.Services;

public class LlmConfig
{
    public string ActiveProvider { get; set; } = "OpenAI";
    public Dictionary<string, LlmProviderConfig> Providers { get; set; } = new();
}

public class LlmProviderConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? DeploymentName { get; set; }
    public string? ApiVersion { get; set; }
}
