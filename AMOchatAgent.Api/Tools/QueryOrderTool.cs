using System.Text.Json;

namespace AMOchatAgent.Api.Tools;

public class QueryOrderTool : ITool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<QueryOrderTool> _logger;

    public QueryOrderTool(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<QueryOrderTool> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public string Name => "query_order";
    public string Description => "查询订单详情和当前状态。需要提供订单ID。";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            order_id = new { type = "string", description = "订单ID" }
        },
        required = new[] { "order_id" }
    };

    public async Task<string> ExecuteAsync(JsonElement parameters)
    {
        var baseUrl = _config["MockApi:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5001";
        var client = _httpClientFactory.CreateClient("MockApi");
        var orderId = parameters.TryGetProperty("order_id", out var id) ? id.GetString() : "";

        _logger.LogInformation("Calling QueryOrder: {OrderId}", orderId);

        try
        {
            var response = await client.GetAsync($"{baseUrl}/api/orders/{orderId}");
            var result = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("QueryOrder response {Status}: {Result}", response.StatusCode, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QueryOrder failed");
            return JsonSerializer.Serialize(new
            {
                success = false,
                errorCode = "NETWORK_ERROR",
                message = "查询失败，网络异常"
            });
        }
    }
}
