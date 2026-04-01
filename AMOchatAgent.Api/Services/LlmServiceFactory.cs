namespace AMOchatAgent.Api.Services;

public class LlmServiceFactory
{
    private readonly IServiceProvider _sp;
    private readonly LlmConfig _config;

    public LlmServiceFactory(IServiceProvider sp, LlmConfig config)
    {
        _sp = sp;
        _config = config;
    }

    public ILlmService Create()
    {
        var providerName = _config.ActiveProvider;
        if (!_config.Providers.TryGetValue(providerName, out var providerConfig))
            throw new InvalidOperationException($"LLM provider '{providerName}' not configured.");

        var httpClientFactory = _sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient($"Llm_{providerName}");
        var logger = _sp.GetRequiredService<ILogger<OpenAiCompatibleLlmService>>();
        var loggerFactory = _sp.GetRequiredService<ILoggerFactory>();
        return new OpenAiCompatibleLlmService(httpClient, providerConfig, providerName, logger, loggerFactory);
    }
}
