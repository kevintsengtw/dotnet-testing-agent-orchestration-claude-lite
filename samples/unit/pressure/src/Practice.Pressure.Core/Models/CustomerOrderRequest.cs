namespace Practice.Pressure.Core.Models;

/// <summary>
/// 訂單建立請求
/// </summary>
public class CustomerOrderRequest
{
    public required string OrderId { get; init; }

    public required string CustomerId { get; init; }

    public required string Sku { get; init; }

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public required string CurrencyCode { get; init; }
}
