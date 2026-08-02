namespace Practice.Pressure.Core.Models;

/// <summary>
/// 庫存調撥結果
/// </summary>
public class InventoryAllocationResult
{
    public bool Success { get; }

    public int RemainingStock { get; }

    public string? FailureReason { get; }

    private InventoryAllocationResult(bool success, int remainingStock, string? failureReason)
    {
        Success = success;
        RemainingStock = remainingStock;
        FailureReason = failureReason;
    }

    public static InventoryAllocationResult Succeeded(int remainingStock) =>
        new(true, remainingStock, null);

    public static InventoryAllocationResult Failed(string reason) =>
        new(false, 0, reason);
}
