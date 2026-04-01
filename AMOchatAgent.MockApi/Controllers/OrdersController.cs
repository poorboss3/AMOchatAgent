using AMOchatAgent.MockApi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace AMOchatAgent.MockApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, OrderDto> Orders = new();

    private static readonly Dictionary<string, (string Name, decimal Price, int Stock)> Products = new()
    {
        ["P001"] = ("iPhone 16", 6999m, 10),
        ["P002"] = ("小米14", 3999m, 50),
        ["P003"] = ("AirPods Pro", 1799m, 100),
        ["P004"] = ("充电宝", 99m, 200),
    };

    private readonly ILogger<OrdersController> _logger;

    public OrdersController(ILogger<OrdersController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public IActionResult CreateOrder([FromBody] CreateOrderRequest request)
    {
        _logger.LogInformation("CreateOrder: ProductId={ProductId}, Qty={Qty}, Phone={Phone}",
            request.ProductId, request.Quantity, request.ReceiverPhone);

        // Validate phone
        if (!Regex.IsMatch(request.ReceiverPhone ?? "", @"^1[3-9]\d{9}$"))
            return Ok(new
            {
                success = false,
                errorCode = "INVALID_PHONE",
                message = "手机号格式不正确，请提供11位有效手机号（以1开头）"
            });

        // Validate product
        if (!Products.TryGetValue(request.ProductId ?? "", out var product))
            return Ok(new
            {
                success = false,
                errorCode = "INVALID_PRODUCT",
                message = $"商品ID '{request.ProductId}' 不存在。可选商品：P001(iPhone 16), P002(小米14), P003(AirPods Pro), P004(充电宝)"
            });

        // Validate quantity
        if (request.Quantity <= 0)
            return Ok(new
            {
                success = false,
                errorCode = "INVALID_QUANTITY",
                message = "数量必须大于0"
            });

        if (request.Quantity > product.Stock)
            return Ok(new
            {
                success = false,
                errorCode = "OUT_OF_STOCK",
                message = $"库存不足，{product.Name} 当前库存为 {product.Stock} 件"
            });

        if (request.Quantity > 10)
            return Ok(new
            {
                success = false,
                errorCode = "QUANTITY_EXCEEDED",
                message = "单次购买数量不能超过10件"
            });

        // Validate receiver name
        if (string.IsNullOrWhiteSpace(request.ReceiverName))
            return Ok(new
            {
                success = false,
                errorCode = "INVALID_NAME",
                message = "收货人姓名不能为空"
            });

        // Validate address
        if (string.IsNullOrWhiteSpace(request.ReceiverAddress) || request.ReceiverAddress.Length < 10)
            return Ok(new
            {
                success = false,
                errorCode = "ADDRESS_TOO_SHORT",
                message = "收货地址太短，请提供完整的省市区街道信息"
            });

        // KYC check for high-value orders
        var amount = product.Price * request.Quantity;
        if (amount > 5000)
        {
            // Simulate: phones ending in odd digit are not KYC verified
            var lastDigit = (request.ReceiverPhone ?? "0")[^1] - '0';
            if (lastDigit % 2 != 0)
                return Ok(new
                {
                    success = false,
                    errorCode = "KYC_REQUIRED",
                    message = $"订单金额 ¥{amount:F2} 超过5000元，需要完成实名认证。请前往APP完成实名认证后重试，或更换其他收货人手机号。"
                });
        }

        // Create order
        var orderId = $"ORD{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
        var order = new OrderDto
        {
            OrderId = orderId,
            ProductId = request.ProductId ?? "",
            ProductName = product.Name,
            Quantity = request.Quantity,
            Amount = amount,
            Status = "pending",
            ReceiverName = request.ReceiverName,
            ReceiverPhone = request.ReceiverPhone ?? "",
            ReceiverAddress = request.ReceiverAddress,
            TrackingNo = $"SF{Random.Shared.Next(1000000000, 1999999999)}",
            CreatedAt = DateTime.UtcNow
        };

        Orders[orderId] = order;
        _logger.LogInformation("Order created: {OrderId}", orderId);

        return Ok(new
        {
            success = true,
            orderId = order.OrderId,
            productName = product.Name,
            amount = order.Amount,
            status = order.Status,
            trackingNo = order.TrackingNo,
            message = $"订单创建成功！订单号：{orderId}，商品：{product.Name} x{request.Quantity}，金额：¥{amount:F2}"
        });
    }

    [HttpGet("{orderId}")]
    public IActionResult GetOrder(string orderId)
    {
        _logger.LogInformation("GetOrder: {OrderId}", orderId);

        if (!Orders.TryGetValue(orderId, out var order))
            return Ok(new
            {
                success = false,
                errorCode = "ORDER_NOT_FOUND",
                message = $"订单 {orderId} 不存在"
            });

        return Ok(new { success = true, order });
    }

    [HttpDelete("{orderId}")]
    public IActionResult CancelOrder(string orderId, [FromQuery] string? reason)
    {
        _logger.LogInformation("CancelOrder: {OrderId}, Reason: {Reason}", orderId, reason);

        if (!Orders.TryGetValue(orderId, out var order))
            return Ok(new
            {
                success = false,
                errorCode = "ORDER_NOT_FOUND",
                message = $"订单 {orderId} 不存在"
            });

        if (order.Status != "pending")
            return Ok(new
            {
                success = false,
                errorCode = "CANCEL_NOT_ALLOWED",
                message = $"订单状态为 '{order.Status}'，无法取消。只有待处理状态的订单才能取消。"
            });

        order.Status = "cancelled";

        return Ok(new
        {
            success = true,
            orderId,
            message = $"订单 {orderId} 已成功取消",
            reason = reason ?? "用户主动取消"
        });
    }
}
