namespace Practice.Pressure.Core.Interfaces;

public interface ITaxCalculationService
{
    Task<decimal> GetExchangeRateAsync(string currencyCode);

    Task<decimal> CalculateTaxAsync(decimal amount, string currencyCode);
}
