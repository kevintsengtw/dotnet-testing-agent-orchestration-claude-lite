namespace Practice.Pressure.Core.Models;

/// <summary>
/// 通知派送結果
/// </summary>
public class DispatchResult
{
    public bool Succeeded { get; }

    public IReadOnlyList<string> FailedChannels { get; }

    public int AttemptCount { get; }

    private DispatchResult(bool succeeded, IReadOnlyList<string> failedChannels, int attemptCount)
    {
        Succeeded = succeeded;
        FailedChannels = failedChannels;
        AttemptCount = attemptCount;
    }

    public static DispatchResult Create(bool succeeded, IReadOnlyList<string> failedChannels, int attemptCount) =>
        new(succeeded, failedChannels, attemptCount);
}
