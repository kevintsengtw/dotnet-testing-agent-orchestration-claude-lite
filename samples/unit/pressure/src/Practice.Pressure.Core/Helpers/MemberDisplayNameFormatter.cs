namespace Practice.Pressure.Core.Helpers;

/// <summary>
/// 會員顯示名稱組合規則：姓在前、名在後，符合客服畫面既有慣例
/// </summary>
public sealed class MemberDisplayNameFormatter
{
    public string Format(string firstName, string lastName)
    {
        var trimmedFirst = firstName?.Trim() ?? string.Empty;
        var trimmedLast = lastName?.Trim() ?? string.Empty;

        if (trimmedFirst.Length == 0 && trimmedLast.Length == 0)
            return "未知會員";

        if (trimmedLast.Length == 0)
            return trimmedFirst;

        if (trimmedFirst.Length == 0)
            return trimmedLast;

        return $"{trimmedLast} {trimmedFirst}";
    }
}
