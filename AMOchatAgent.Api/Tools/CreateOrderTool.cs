using System.Text;
using System.Text.Json;
using AMOchatAgent.Api.Models;

namespace AMOchatAgent.Api.Tools;

public class CreateOrderTool : ITool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<CreateOrderTool> _logger;

    public CreateOrderTool(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<CreateOrderTool> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public string Name => "create_order";
    public string Description => "创建订单。需要提供商品ID、数量、收货人姓名、手机号和地址。当用户提供了所有必要信息后调用此工具。";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            product_id = new { type = "string", description = "商品ID" },
            quantity = new { type = "integer", description = "购买数量，必须大于0" },
            receiver_name = new { type = "string", description = "收货人姓名" },
            receiver_phone = new { type = "string", description = "收货人手机号（11位）" },
            receiver_address = new { type = "string", description = "完整收货地址（省市区街道门牌号）" }
        },
        required = new[] { "product_id", "quantity", "receiver_name", "receiver_phone", "receiver_address" }
    };

    public async Task<string> ExecuteAsync(JsonElement parameters)
    {
        var baseUrl = _config["MockApi:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5001";
        var client = _httpClientFactory.CreateClient("MockApi");

        var body = JsonSerializer.Serialize(new
        {
            productId = parameters.TryGetProperty("product_id", out var pid) ? pid.GetString() : "",
            quantity = parameters.TryGetProperty("quantity", out var qty) ? qty.GetInt32() : 0,
            receiverName = parameters.TryGetProperty("receiver_name", out var rn) ? rn.GetString() : "",
            receiverPhone = parameters.TryGetProperty("receiver_phone", out var rp) ? rp.GetString() : "",
            receiverAddress = parameters.TryGetProperty("receiver_address", out var ra) ? ra.GetString() : ""
        });

        _logger.LogInformation("Calling CreateOrder: {Body}", body);

        try
        {
            var response = await client.PostAsync(
                $"{baseUrl}/api/orders",
                new StringContent(body, Encoding.UTF8, "application/json"));

            var result = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("CreateOrder response {Status}: {Result}", response.StatusCode, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateOrder failed");
            return JsonSerializer.Serialize(new
            {
                success = false,
                errorCode = "NETWORK_ERROR",
                message = "网络异常，请稍后重试"
            });
        }
    }
}
