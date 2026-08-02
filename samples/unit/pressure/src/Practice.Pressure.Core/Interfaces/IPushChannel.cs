using Practice.Pressure.Core.Models;

namespace Practice.Pressure.Core.Interfaces;

public interface IPushChannel
{
    Task<bool> SendAsync(NotificationRequest request, CancellationToken cancellationToken);
}
