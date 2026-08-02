using Practice.Pressure.Core.Helpers;
using Practice.Pressure.Core.Interfaces;
using Practice.Pressure.Core.Models;

namespace Practice.Pressure.Core;

/// <summary>
/// 會員檔案查詢服務，橫跨客服畫面與後台報表共用，部分方法比 TimeProvider 導入還早，
/// 一直沒有機會補回去。
/// </summary>
public class LegacyMemberProfileService
{
    private readonly IMemberRepository _repository;
    private readonly IMemberNotifier _notifier;
    private readonly MemberDisplayNameFormatter _formatter;

    public LegacyMemberProfileService(
        IMemberRepository repository,
        IMemberNotifier notifier,
        MemberDisplayNameFormatter formatter)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
    }

    public MemberProfileSummary? GetDisplayProfile(string memberId)
    {
        var profile = _repository.FindById(memberId);
        if (profile is null)
            return null;

        var displayName = _formatter.Format(profile.FirstName, profile.LastName);
        var tier = MemberHelper.ComputeTier(profile.TotalSpend);
        var regionName = MemberHelper.GetRegionDisplayName(profile.RegionCode);

        return new MemberProfileSummary(displayName, tier, regionName);
    }

    public MemberTier? GetCurrentTier(string memberId)
    {
        var profile = _repository.FindById(memberId);
        return profile is null ? null : MemberHelper.ComputeTier(profile.TotalSpend);
    }

    public string FormatDisplayName(string firstName, string lastName)
    {
        return _formatter.Format(firstName, lastName);
    }

    public string GetRegionDisplayName(string regionCode)
    {
        return MemberHelper.GetRegionDisplayName(regionCode);
    }

    public int GetAccountAgeDays(string memberId)
    {
        var profile = _repository.FindById(memberId);
        if (profile is null)
            return -1;

        return (int)(DateTime.Now - profile.JoinedAt).TotalDays;
    }

    public bool NotifyIfEligibleForUpgrade(string memberId)
    {
        var profile = _repository.FindById(memberId);
        if (profile is null)
            return false;

        var computedTier = MemberHelper.ComputeTier(profile.TotalSpend);
        if (computedTier <= profile.RecordedTier)
            return false;

        _notifier.NotifyTierUpgrade(memberId, computedTier, DateTime.Now);
        return true;
    }
}
