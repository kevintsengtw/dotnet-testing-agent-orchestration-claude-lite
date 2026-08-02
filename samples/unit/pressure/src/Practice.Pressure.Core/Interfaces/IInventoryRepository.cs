namespace Practice.Pressure.Core.Interfaces;

public interface IInventoryRepository
{
    Task<int> GetStockAsync(string sku);

    Task DecrementStockAsync(string sku, int quantity);

    Task IncrementStockAsync(string sku, int quantity);
}
