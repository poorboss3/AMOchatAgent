using System.Text.Json;

namespace AMOchatAgent.Api.Tools;

public class CheckUserKycTool : ITool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<CheckUserKycTool> _logger;

    public CheckUserKycTool(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<CheckUserKycTool> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public string Name => "check_user_kyc";
    public string Description => "查询用户实名认证（KYC）状态。需要提供用户手机号。高金额订单（超过5000元）需要完成实名认证。";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            phone = new { type = "string", description = "用户手机号（11位）" }
        },
        required = new[] { "phone" }
    };

    public async Task<string> ExecuteAsync(JsonElement parameters)
    {
        var baseUrl = _config["MockApi:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5001";
        var client = _httpClientFactory.CreateClient("MockApi");
        var phone = parameters.TryGetProperty("phone", out var p) ? p.GetString() : "";

        _logger.LogInformation("Calling CheckUserKyc: {Phone}", phone);

        try
        {
            var response = await client.GetAsync($"{baseUrl}/api/users/{phone}/kyc");
            var result = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("CheckUserKyc response {Status}: {Result}", response.StatusCode, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CheckUserKyc failed");
            return JsonSerializer.Serialize(new
            {
                success = false,
                errorCode = "NETWORK_ERROR",
                message = "查询认证状态失败，网络异常，请稍后重试"
            });
        }
    }
}
