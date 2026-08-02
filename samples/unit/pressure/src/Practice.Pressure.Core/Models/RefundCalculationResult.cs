namespace Practice.Pressure.Core.Models;

/// <summary>
/// 退款試算結果
/// </summary>
public class RefundCalculationResult
{
    public bool IsEligible { get; }

    public decimal ApprovedAmount { get; }

    public string? Reason { get; }

    private RefundCalculationResult(bool isEligible, decimal approvedAmount, string? reason)
    {
        IsEligible = isEligible;
        ApprovedAmount = approvedAmount;
        Reason = reason;
    }

    public static RefundCalculationResult Eligible(decimal approvedAmount) =>
        new(true, approvedAmount, null);

    public static RefundCalculationResult NotEligible(string reason) =>
        new(false, 0m, reason);
}
