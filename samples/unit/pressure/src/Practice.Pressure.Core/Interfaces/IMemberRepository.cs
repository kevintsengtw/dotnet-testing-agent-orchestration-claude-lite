using Practice.Pressure.Core.Models;

namespace Practice.Pressure.Core.Interfaces;

public interface IMemberRepository
{
    MemberProfile? FindById(string memberId);
}
