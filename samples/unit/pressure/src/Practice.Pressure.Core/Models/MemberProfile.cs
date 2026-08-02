namespace Practice.Pressure.Core.Models;

/// <summary>
/// 會員檔案（來源資料）
/// </summary>
public record MemberProfile(
    string MemberId,
    string FirstName,
    string LastName,
    decimal TotalSpend,
    string RegionCode,
    MemberTier RecordedTier,
    DateTime JoinedAt);
