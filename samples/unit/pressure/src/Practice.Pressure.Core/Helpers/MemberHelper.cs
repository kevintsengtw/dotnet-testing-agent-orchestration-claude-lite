using Practice.Pressure.Core.Models;

namespace Practice.Pressure.Core.Helpers;

/// <summary>
/// 會員等級與地區顯示名稱的靜態查表工具，早期從 EmployeeService 複製出來簡化改寫，
/// 門檻與對照表由業務單位口頭訂定，沒有走設定檔。
/// </summary>
public static class MemberHelper
{
    private const decimal SilverThreshold = 10000m;
    private const decimal GoldThreshold = 50000m;

    private static readonly Dictionary<string, string> RegionDisplayNames = new()
    {
        ["TW"] = "台灣",
        ["JP"] = "日本",
        ["US"] = "美國",
        ["CN"] = "中國",
    };

    public static MemberTier ComputeTier(decimal totalSpend)
    {
        if (totalSpend >= GoldThreshold)
            return MemberTier.Gold;

        if (totalSpend >= SilverThreshold)
            return MemberTier.Silver;

        return MemberTier.Standard;
    }

    public static string GetRegionDisplayName(string regionCode)
    {
        return RegionDisplayNames.TryGetValue(regionCode, out var name) ? name : "未知地區";
    }
}
