using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AMOchatAgent.Api.Models;

namespace AMOchatAgent.Api.Services;

public class OpenAiCompatibleLlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly LlmProviderConfig _config;
    private readonly string _providerName;
    private readonly ILogger<OpenAiCompatibleLlmService> _logger;
    private readonly ILogger _httpLogger;

    public OpenAiCompatibleLlmService(
        HttpClient httpClient,
        LlmProviderConfig config,
        string providerName,
        ILogger<OpenAiCompatibleLlmService> logger,
        ILoggerFactory loggerFactory)
    {
        _httpClient = httpClient;
        _config = config;
        _providerName = providerName;
        _logger = logger;
        _httpLogger = loggerFactory.CreateLogger("LlmHttp");

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config.ApiKey);

        if (providerName == "AzureOpenAI")
            _httpClient.DefaultRequestHeaders.Add("api-key", config.ApiKey);
    }

    private const int MaxRetries = 4;

    private async Task<(HttpResponseMessage Response, string Body)> PostWithRetryAsync(
        string url, string json, CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; ; attempt++)
        {
            var response = await _httpClient.PostAsync(
                url,
                new StringContent(json, Encoding.UTF8, "application/json"),
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if ((int)response.StatusCode != 429 || attempt >= MaxRetries)
                return (response, body);

            var delay = response.Headers.RetryAfter?.Delta
                        ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
            _httpLogger.LogInformation(
                "RETRY attempt={Attempt}/{MaxRetries} waitSeconds={Delay} reason=429_RateLimit",
                attempt + 1, MaxRetries, delay.TotalSeconds);
            _logger.LogWarning("Rate limited (429) by {Provider}. Retry {Attempt}/{MaxRetries} in {Delay}s",
                _providerName, attempt + 1, MaxRetries, delay.TotalSeconds);
            await Task.Delay(delay, cancellationToken);
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        string url, string json, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if ((int)response.StatusCode != 429 || attempt >= MaxRetries)
                return response;

            response.Dispose();
            var delay = response.Headers.RetryAfter?.Delta
                        ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
            _httpLogger.LogInformation(
                "RETRY attempt={Attempt}/{MaxRetries} waitSeconds={Delay} reason=429_RateLimit",
                attempt + 1, MaxRetries, delay.TotalSeconds);
            _logger.LogWarning("Rate limited (429) by {Provider}. Retry {Attempt}/{MaxRetries} in {Delay}s",
                _providerName, attempt + 1, MaxRetries, delay.TotalSeconds);
            await Task.Delay(delay, cancellationToken);
        }
    }

    private string BuildChatUrl()
    {
        var baseUrl = _config.BaseUrl.TrimEnd('/');
        if (_providerName == "AzureOpenAI")
        {
            var deployment = _config.DeploymentName ?? _config.Model;
            var apiVersion = _config.ApiVersion ?? "2024-02-01";
            return $"{baseUrl}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
        }
        return $"{baseUrl}/chat/completions";
    }

    public async Task<LlmMessage> ChatAsync(List<LlmMessage> messages, List<ToolDefinition>? tools = null)
    {
        var request = new LlmChatRequest
        {
            Model = _config.Model,
            Messages = messages,
            Tools = tools?.Count > 0 ? tools : null,
            ToolChoice = tools?.Count > 0 ? "auto" : null
        };

        var serializeOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var url = BuildChatUrl();
        var json = JsonSerializer.Serialize(request, serializeOptions);

        _httpLogger.LogInformation(
            "REQUEST provider={Provider} model={Model} url={Url} body={Body}",
            _providerName, request.Model, url, json);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var (response, responseBody) = await PostWithRetryAsync(url, json);
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _httpLogger.LogError(
                "RESPONSE provider={Provider} status={Status} duration={Duration}ms body={Body}",
                _providerName, (int)response.StatusCode, sw.ElapsedMilliseconds, responseBody);
            _logger.LogError("LLM API error {Status} from {Provider}", (int)response.StatusCode, _providerName);
            throw new HttpRequestException($"LLM API returned {response.StatusCode}: {responseBody}");
        }

        var llmResponse = JsonSerializer.Deserialize<LlmChatResponse>(
            responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var choice = llmResponse?.Choices?.FirstOrDefault();
        var toolCallNames = choice?.Message?.ToolCalls?
            .Select(tc => tc.Function.Name)
            .ToList() ?? new List<string>();

        _httpLogger.LogInformation(
            "RESPONSE provider={Provider} status={Status} duration={Duration}ms finish={FinishReason} toolCalls=[{ToolCalls}] tokens={PromptTokens}+{CompletionTokens}={TotalTokens} body={Body}",
            _providerName, (int)response.StatusCode, sw.ElapsedMilliseconds,
            choice?.FinishReason ?? "unknown",
            string.Join(", ", toolCallNames),
            llmResponse?.Usage?.PromptTokens ?? 0,
            llmResponse?.Usage?.CompletionTokens ?? 0,
            llmResponse?.Usage?.TotalTokens ?? 0,
            responseBody);

        return choice?.Message
               ?? new LlmMessage { Role = "assistant", Content = "抱歉，我遇到了问题，请稍后重试。" };
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        List<LlmMessage> messages,
        List<ToolDefinition>? tools = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new LlmChatRequest
        {
            Model = _config.Model,
            Messages = messages,
            Tools = tools?.Count > 0 ? tools : null,
            ToolChoice = tools?.Count > 0 ? "auto" : null,
            Stream = true
        };

        var serializeOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(request, serializeOptions);
        var url = BuildChatUrl();

        using var response = await SendWithRetryAsync(url, json, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            string? delta = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var deltaElement = choices[0].GetProperty("delta");
                    if (deltaElement.TryGetProperty("content", out var c))
                        delta = c.GetString();
                }
            }
            catch
            {
                // skip malformed chunks
            }

            if (delta != null)
                yield return delta;
        }
    }
}
