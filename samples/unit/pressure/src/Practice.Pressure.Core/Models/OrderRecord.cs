namespace Practice.Pressure.Core.Models;

/// <summary>
/// 訂單核對用的簡化訂單資料
/// </summary>
public record OrderRecord(string OrderId, string CustomerId, decimal Amount, string Status);
