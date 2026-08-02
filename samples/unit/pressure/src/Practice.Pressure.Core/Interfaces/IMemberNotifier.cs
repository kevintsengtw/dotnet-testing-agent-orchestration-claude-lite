using Practice.Pressure.Core.Models;

namespace Practice.Pressure.Core.Interfaces;

public interface IMemberNotifier
{
    void NotifyTierUpgrade(string memberId, MemberTier newTier, DateTime notifiedAt);
}
