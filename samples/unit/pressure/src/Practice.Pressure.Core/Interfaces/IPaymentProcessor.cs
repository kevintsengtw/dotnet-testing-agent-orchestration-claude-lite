namespace Practice.Pressure.Core.Interfaces;

public interface IPaymentProcessor
{
    Task<bool> ChargeAsync(string customerId, decimal amount);

    Task<bool> RefundAsync(string orderId, decimal amount);
}
