namespace Practice.Pressure.Core.Models;

/// <summary>
/// 給前台顯示用的會員摘要
/// </summary>
public record MemberProfileSummary(string DisplayName, MemberTier Tier, string RegionDisplayName);
