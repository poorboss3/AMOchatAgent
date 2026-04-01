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

    public OpenAiCompatibleLlmService(
        HttpClient httpClient,
        LlmProviderConfig config,
        string providerName,
        ILogger<OpenAiCompatibleLlmService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _providerName = providerName;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config.ApiKey);

        if (providerName == "AzureOpenAI")
            _httpClient.DefaultRequestHeaders.Add("api-key", config.ApiKey);
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

        var json = JsonSerializer.Serialize(request, serializeOptions);
        _logger.LogDebug("LLM request to {Provider}: {Json}", _providerName, json);

        var response = await _httpClient.PostAsync(
            BuildChatUrl(),
            new StringContent(json, Encoding.UTF8, "application/json"));

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("LLM API error {Status}: {Body}", response.StatusCode, responseBody);
            throw new HttpRequestException($"LLM API returned {response.StatusCode}: {responseBody}");
        }

        var llmResponse = JsonSerializer.Deserialize<LlmChatResponse>(
            responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return llmResponse?.Choices?.FirstOrDefault()?.Message
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
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatUrl())
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

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
