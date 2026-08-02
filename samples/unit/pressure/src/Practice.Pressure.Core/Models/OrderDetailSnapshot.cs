namespace Practice.Pressure.Core.Models;

/// <summary>
/// 訂單明細快照（RetrieveOrderDetail 用，跟 OrderRecord 欄位重疊但歷史上分開建的）
/// </summary>
public record OrderDetailSnapshot(string OrderId, string CustomerId, decimal Amount, string Status);
