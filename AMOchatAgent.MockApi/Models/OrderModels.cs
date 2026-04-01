namespace AMOchatAgent.MockApi.Models;

public class OrderAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string DataBase64 { get; set; } = string.Empty;
}

public class CreateOrderRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string ReceiverAddress { get; set; } = string.Empty;
    public List<OrderAttachment>? Attachments { get; set; }
}

public class OrderDto
{
    public string OrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string ReceiverAddress { get; set; } = string.Empty;
    public string TrackingNo { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int AttachmentCount { get; set; }
    public List<string> AttachmentNames { get; set; } = new();
}
