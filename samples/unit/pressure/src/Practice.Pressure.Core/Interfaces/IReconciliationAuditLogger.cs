namespace Practice.Pressure.Core.Interfaces;

/// <summary>
/// 核對異常紀錄
/// </summary>
public interface IReconciliationAuditLogger
{
    void LogMismatch(string orderId, string reason);
}
