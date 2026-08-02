using Practice.Pressure.Core.Interfaces;
using Practice.Pressure.Core.Models;

namespace Practice.Pressure.Core;

/// <summary>
/// 通知派送服務。統一決定要走哪些通道、要不要重試、要不要因為靜音時段延後推播。
/// </summary>
public class NotificationDispatcher
{
    private readonly IPushChannel _pushChannel;
    private readonly IEmailChannel _emailChannel;
    private readonly INotificationAuditRecorder _auditRecorder;
    private readonly TimeProvider _timeProvider;

    private const int MaxRetryCount = 2;

    // 30 秒是配合通道服務商建議的節流閾值
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    // 客服規範訂定的靜音時段：晚間 10 點到隔天早上 7 點
    private const int QuietHoursStart = 22;
    private const int QuietHoursEnd = 7;

    // TODO: 之後可能會加 SMS 通道，目前業務只簽了 Push 跟 Email 兩家供應商

    public NotificationDispatcher(
        IPushChannel pushChannel,
        IEmailChannel emailChannel,
        INotificationAuditRecorder auditRecorder,
        TimeProvider timeProvider)
    {
        _pushChannel = pushChannel ?? throw new ArgumentNullException(nameof(pushChannel));
        _emailChannel = emailChannel ?? throw new ArgumentNullException(nameof(emailChannel));
        _auditRecorder = auditRecorder ?? throw new ArgumentNullException(nameof(auditRecorder));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<DispatchResult> DispatchAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var pushSuppressed = IsWithinQuietHours(_timeProvider.GetLocalNow()) && !request.IsUrgent;

        var pushOk = false;
        var emailOk = false;
        var attemptCount = 0;

        for (var attempt = 0; attempt <= MaxRetryCount; attempt++)
        {
            attemptCount = attempt + 1;

            if (!pushSuppressed && !pushOk)
            {
                pushOk = await InvokeChannelAsync(
                    ct => _pushChannel.SendAsync(request, ct), cancellationToken);
            }

            if (!emailOk)
            {
                emailOk = await InvokeChannelAsync(
                    ct => _emailChannel.SendAsync(request, ct), cancellationToken);
            }

            var pushSettled = pushSuppressed || pushOk;
            if (pushSettled && emailOk)
                break;

            if (attempt < MaxRetryCount)
            {
                await Task.Delay(RetryDelay, _timeProvider, cancellationToken);
            }
        }

        var failedChannels = new List<string>();
        if (!pushSuppressed && !pushOk)
            failedChannels.Add("Push");
        if (!emailOk)
            failedChannels.Add("Email");

        var succeeded = pushOk || emailOk;

        _auditRecorder.Record(request.RequestId, succeeded, failedChannels, attemptCount);

        return DispatchResult.Create(succeeded, failedChannels, attemptCount);
    }

    // 通道逾時（非呼叫端主動取消）視為單次失敗，交由重試機制處理；呼叫端主動取消則往外拋出
    private static async Task<bool> InvokeChannelAsync(
        Func<CancellationToken, Task<bool>> send, CancellationToken cancellationToken)
    {
        try
        {
            return await send(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static bool IsWithinQuietHours(DateTimeOffset now)
    {
        return now.Hour >= QuietHoursStart || now.Hour < QuietHoursEnd;
    }
}
