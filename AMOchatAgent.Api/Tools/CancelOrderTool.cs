using System.Text.Json;

namespace AMOchatAgent.Api.Tools;

public class CancelOrderTool : ITool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<CancelOrderTool> _logger;

    public CancelOrderTool(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<CancelOrderTool> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public string Name => "cancel_order";
    public string Description => "取消订单。需要提供订单ID，可选提供取消原因。只有状态为待处理（pending）的订单才能取消。";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            order_id = new { type = "string", description = "要取消的订单ID" },
            reason = new { type = "string", description = "取消原因（可选）" }
        },
        required = new[] { "order_id" }
    };

    public async Task<string> ExecuteAsync(JsonElement parameters)
    {
        var baseUrl = _config["MockApi:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5001";
        var client = _httpClientFactory.CreateClient("MockApi");

        var orderId = parameters.TryGetProperty("order_id", out var id) ? id.GetString() : "";
        var reason = parameters.TryGetProperty("reason", out var r) ? r.GetString() : null;

        _logger.LogInformation("Calling CancelOrder: {OrderId}, Reason: {Reason}", orderId, reason);

        try
        {
            var url = $"{baseUrl}/api/orders/{orderId}";
            if (!string.IsNullOrWhiteSpace(reason))
                url += $"?reason={Uri.EscapeDataString(reason)}";

            var request = new HttpRequestMessage(HttpMethod.Delete, url);
            var response = await client.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("CancelOrder response {Status}: {Result}", response.StatusCode, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CancelOrder failed");
            return JsonSerializer.Serialize(new
            {
                success = false,
                errorCode = "NETWORK_ERROR",
                message = "取消订单失败，网络异常，请稍后重试"
            });
        }
    }
}
