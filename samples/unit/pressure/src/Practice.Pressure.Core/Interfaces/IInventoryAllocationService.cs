namespace Practice.Pressure.Core.Interfaces;

/// <summary>
/// 外部庫存調度系統（跟 <see cref="IInventoryRepository"/> 不同——
/// 這個是打外部系統做預留，Repository 是本地庫存數字）
/// </summary>
public interface IInventoryAllocationService
{
    Task<bool> ReserveAsync(string sku, int quantity);

    Task ReleaseAsync(string sku, int quantity);
}
