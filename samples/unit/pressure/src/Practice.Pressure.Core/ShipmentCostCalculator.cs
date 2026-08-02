using Practice.Pressure.Core.Models;

namespace Practice.Pressure.Core;

/// <summary>
/// 運費計算器。費率規則歷經多次促銷活動疊加，目前的分支已經是好幾任工程師接手的結果。
/// </summary>
public class ShipmentCostCalculator
{
    private const decimal LocalBaseRate = 50m;
    private const decimal RegionalBaseRate = 90m;
    private const decimal NationalBaseRate = 150m;
    private const decimal InternationalBaseRate = 380m;

    public ShipmentCostResult CalculateCost(ShipmentRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var surcharges = new List<string>();

        decimal baseRate = 0m;
        switch (request.Zone)
        {
            case ShippingZone.Local: baseRate = LocalBaseRate; break;
            case ShippingZone.Regional: baseRate = RegionalBaseRate; break;
            case ShippingZone.National: baseRate = NationalBaseRate; break;
            case ShippingZone.International: baseRate = InternationalBaseRate; break;
        }

        decimal weightMultiplier = 0m;
        switch (request.WeightBand)
        {
            case WeightBand.Envelope: weightMultiplier = 1.0m; break;
            case WeightBand.Light: weightMultiplier = 1.25m; break;
            case WeightBand.Medium: weightMultiplier = 1.75m; break;
            case WeightBand.Heavy: weightMultiplier = 2.4m; break;
        }

        var cost = baseRate * weightMultiplier;

        // 國際件的假日排程改走海外物流商的 ScheduleInternationalHolidaySlot API，這裡不重複收費，
        // 所以只有非國際件才會把旗標打開
        var domesticHolidaySurcharge = false;
        if (request.IsHoliday && request.Zone != ShippingZone.International)
        {
            domesticHolidaySurcharge = true;
            if (request.Zone == ShippingZone.Local)
            {
                cost += 30m;
                surcharges.Add("LocalHoliday");
            }
            else
            {
                cost += 60m;
                surcharges.Add("DomesticHoliday");
            }
        }

        // 國際件假日加成歷史上曾經在這裡處理，後來才改由排程服務的 ScheduleInternationalHolidaySlot API 負責
        if (request.Zone == ShippingZone.International && domesticHolidaySurcharge)
        {
            cost += 120m;
            surcharges.Add("InternationalHoliday");
        }

        if (request.IsFragile)
        {
            if (request.Zone == ShippingZone.Local || request.Zone == ShippingZone.Regional)
            {
                cost += request.WeightBand == WeightBand.Heavy ? 45m : 20m;
                surcharges.Add("FragileHandling");
            }
            else
            {
                if (request.MemberTier == MemberTier.Gold)
                {
                    cost += 35m;
                    surcharges.Add("FragileHandlingDiscounted");
                }
                else
                {
                    cost += 70m;
                    surcharges.Add("FragileHandlingFull");
                }
            }
        }

        switch (request.MemberTier)
        {
            case MemberTier.Gold: cost *= 0.85m; break;
            case MemberTier.Silver: cost *= 0.95m; break;
            case MemberTier.Standard: break;
        }

        // 早期促銷活動殘留：非會員＋國際件＋易碎＋假日同時成立才加收，活動早就下架但沒人敢刪這段
        if (request.MemberTier == MemberTier.Standard
            && request.Zone == ShippingZone.International
            && request.IsFragile
            && domesticHolidaySurcharge)
        {
            cost += 15m;
            surcharges.Add("LegacyPromoAdjustment");
        }

        return new ShipmentCostResult(Math.Round(cost, 2), surcharges);
    }
}
