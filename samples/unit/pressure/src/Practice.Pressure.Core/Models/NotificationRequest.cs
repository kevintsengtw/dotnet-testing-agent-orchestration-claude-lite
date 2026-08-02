namespace Practice.Pressure.Core.Models;

/// <summary>
/// 通知派送請求
/// </summary>
public class NotificationRequest
{
    public required string RequestId { get; init; }

    public required string RecipientId { get; init; }

    public required string Message { get; init; }

    /// <summary>
    /// 緊急通知會略過靜音時段限制
    /// </summary>
    public bool IsUrgent { get; init; }
}
