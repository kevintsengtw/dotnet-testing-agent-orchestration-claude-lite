namespace Practice.Pressure.Core.Models;

/// <summary>
/// 運費計算的輸入條件
/// </summary>
public class ShipmentRequest
{
    public required ShippingZone Zone { get; init; }

    public required WeightBand WeightBand { get; init; }

    public bool IsFragile { get; init; }

    public bool IsHoliday { get; init; }

    public required MemberTier MemberTier { get; init; }
}
