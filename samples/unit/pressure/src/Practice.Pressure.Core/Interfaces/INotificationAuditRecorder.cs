namespace Practice.Pressure.Core.Interfaces;

public interface INotificationAuditRecorder
{
    void Record(string requestId, bool succeeded, IReadOnlyList<string> failedChannels, int attemptCount);
}
