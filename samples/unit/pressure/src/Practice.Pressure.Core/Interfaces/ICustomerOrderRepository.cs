using Practice.Pressure.Core.Models;

namespace Practice.Pressure.Core.Interfaces;

public interface ICustomerOrderRepository
{
    Task SaveAsync(CustomerOrderRequest request, decimal totalAmount);

    Task<IReadOnlyList<string>> GetPendingOrderIdsAsync();

    Task<(int OrderCount, decimal TotalRevenue)> GetMonthlySalesAsync(int year, int month);
}
