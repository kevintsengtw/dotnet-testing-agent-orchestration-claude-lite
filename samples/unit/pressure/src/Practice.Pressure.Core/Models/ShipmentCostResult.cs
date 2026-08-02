namespace Practice.Pressure.Core.Models;

/// <summary>
/// 運費計算結果
/// </summary>
public class ShipmentCostResult
{
    public decimal TotalCost { get; }

    public IReadOnlyList<string> AppliedSurcharges { get; }

    public ShipmentCostResult(decimal totalCost, IReadOnlyList<string> appliedSurcharges)
    {
        TotalCost = totalCost;
        AppliedSurcharges = appliedSurcharges;
    }
}
