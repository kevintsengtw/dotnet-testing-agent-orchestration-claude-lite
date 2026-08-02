using Practice.Pressure.Core.Models;

namespace Practice.Pressure.Core.Interfaces;

/// <summary>
/// 訂單核對用的資料存取介面
/// </summary>
public interface IReconciliationOrderStore
{
    OrderRecord? FindById(string orderId);

    IReadOnlyList<OrderRecord> FindByCustomer(string customerId);

    bool MarkReconciled(string orderId);
}
