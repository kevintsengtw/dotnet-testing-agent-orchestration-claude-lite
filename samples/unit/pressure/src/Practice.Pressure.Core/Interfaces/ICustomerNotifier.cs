namespace Practice.Pressure.Core.Interfaces;

public interface ICustomerNotifier
{
    Task NotifyOrderCreatedAsync(string customerId, string orderId);

    Task NotifyRefundProcessedAsync(string customerId, string orderId, decimal amount);
}
