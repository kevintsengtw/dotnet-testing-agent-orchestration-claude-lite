namespace Practice.Pressure.Core.Interfaces;

public interface IPricingCache
{
    bool TryGetPrice(string sku, out decimal price);

    void SetPrice(string sku, decimal price);
}
