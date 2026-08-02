using Practice.Pressure.Core.Models;

namespace Practice.Pressure.Core.Interfaces;

public interface IEmailChannel
{
    Task<bool> SendAsync(NotificationRequest request, CancellationToken cancellationToken);
}
