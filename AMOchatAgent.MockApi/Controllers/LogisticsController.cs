using Microsoft.AspNetCore.Mvc;

namespace AMOchatAgent.MockApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogisticsController : ControllerBase
{
    private readonly ILogger<LogisticsController> _logger;

    public LogisticsController(ILogger<LogisticsController> logger)
    {
        _logger = logger;
    }

    [HttpGet("{trackingNo}")]
    public IActionResult GetLogistics(string trackingNo)
    {
        _logger.LogInformation("GetLogistics: {TrackingNo}", trackingNo);

        if (string.IsNullOrWhiteSpace(trackingNo) || !trackingNo.StartsWith("SF"))
            return Ok(new
            {
                success = false,
                errorCode = "INVALID_TRACKING",
                message = "运单号格式不正确，顺丰运单号以SF开头"
            });

        // Mock logistics data
        var now = DateTime.UtcNow;
        var events = new[]
        {
            new
            {
                time = now.AddHours(-4).ToString("yyyy-MM-dd HH:mm"),
                location = "上海揽收网点",
                status = "已揽收",
                description = "快件已由顺丰快递员揽收"
            },
            new
            {
                time = now.AddHours(-3).ToString("yyyy-MM-dd HH:mm"),
                location = "上海转运中心",
                status = "已到达",
                description = "快件已到达上海转运中心"
            },
            new
            {
                time = now.AddHours(-2).ToString("yyyy-MM-dd HH:mm"),
                location = "上海浦东分拨中心",
                status = "运输中",
                description = "快件已从上海转运中心发出，正在运输途中"
            },
            new
            {
                time = now.AddHours(-1).ToString("yyyy-MM-dd HH:mm"),
                location = "目的城市转运中心",
                status = "已到达",
                description = "快件已到达目的城市转运中心"
            },
            new
            {
                time = now.AddMinutes(-30).ToString("yyyy-MM-dd HH:mm"),
                location = "目的地派送站",
                status = "派送中",
                description = "快件已交由快递员派送，请保持手机畅通"
            },
        };

        return Ok(new
        {
            success = true,
            trackingNo,
            carrier = "顺丰速递",
            currentStatus = "派送中",
            estimatedDelivery = now.AddHours(2).ToString("yyyy-MM-dd HH:mm"),
            events
        });
    }
}
