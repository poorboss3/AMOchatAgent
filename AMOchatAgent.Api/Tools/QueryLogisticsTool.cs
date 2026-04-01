using System.Text.Json;

namespace AMOchatAgent.Api.Tools;

public class QueryLogisticsTool : ITool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<QueryLogisticsTool> _logger;

    public QueryLogisticsTool(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<QueryLogisticsTool> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public string Name => "query_logistics";
    public string Description => "查询快递物流信息。需要提供运单号（快递单号）。可以通过查询订单获取运单号。";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            tracking_no = new { type = "string", description = "快递运单号" }
        },
        required = new[] { "tracking_no" }
    };

    public async Task<string> ExecuteAsync(JsonElement parameters)
    {
        var baseUrl = _config["MockApi:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5001";
        var client = _httpClientFactory.CreateClient("MockApi");
        var trackingNo = parameters.TryGetProperty("tracking_no", out var tn) ? tn.GetString() : "";

        _logger.LogInformation("Calling QueryLogistics: {TrackingNo}", trackingNo);

        try
        {
            var response = await client.GetAsync($"{baseUrl}/api/logistics/{trackingNo}");
            var result = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("QueryLogistics response {Status}: {Result}", response.StatusCode, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QueryLogistics failed");
            return JsonSerializer.Serialize(new
            {
                success = false,
                errorCode = "NETWORK_ERROR",
                message = "查询物流失败，网络异常，请稍后重试"
            });
        }
    }
}
