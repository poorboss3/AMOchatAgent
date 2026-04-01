using Microsoft.AspNetCore.Mvc;

namespace AMOchatAgent.MockApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> _logger;

    public UsersController(ILogger<UsersController> logger)
    {
        _logger = logger;
    }

    [HttpGet("{phone}/kyc")]
    public IActionResult CheckKyc(string phone)
    {
        _logger.LogInformation("CheckKyc: {Phone}", phone);

        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 11)
            return Ok(new
            {
                success = false,
                errorCode = "INVALID_PHONE",
                message = "手机号格式不正确，请提供11位手机号"
            });

        // Simulate: phones ending in even digit are KYC verified
        var lastDigit = phone[^1] - '0';
        var isVerified = lastDigit % 2 == 0;

        return Ok(new
        {
            success = true,
            phone,
            kycVerified = isVerified,
            level = isVerified ? "L2" : "L0",
            verifiedAt = isVerified ? DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd") : (string?)null,
            message = isVerified
                ? "实名认证已完成，可以进行高金额订单"
                : "尚未完成实名认证，单笔订单金额不能超过5000元。请前往APP进行实名认证。"
        });
    }
}
