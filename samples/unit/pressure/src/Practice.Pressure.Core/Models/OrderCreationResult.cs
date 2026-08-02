namespace Practice.Pressure.Core.Models;

/// <summary>
/// 訂單建立結果
/// </summary>
public class OrderCreationResult
{
    public bool Success { get; }

    public string? OrderId { get; }

    public decimal? TotalAmount { get; }

    public string? FailureReason { get; }

    private OrderCreationResult(bool success, string? orderId, decimal? totalAmount, string? failureReason)
    {
        Success = success;
        OrderId = orderId;
        TotalAmount = totalAmount;
        FailureReason = failureReason;
    }

    public static OrderCreationResult Succeeded(string orderId, decimal totalAmount) =>
        new(true, orderId, totalAmount, null);

    public static OrderCreationResult Failed(string reason) =>
        new(false, null, null, reason);
}
