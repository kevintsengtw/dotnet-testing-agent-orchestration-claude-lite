namespace Practice.Pressure.Core.Interfaces;

public interface IRefundRecordRepository
{
    Task<decimal> GetTotalRefundedAsync(string orderId);

    Task RecordRefundAsync(string orderId, decimal amount);
}
