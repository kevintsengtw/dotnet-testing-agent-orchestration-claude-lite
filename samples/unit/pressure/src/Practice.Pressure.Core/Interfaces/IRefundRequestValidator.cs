namespace Practice.Pressure.Core.Interfaces;

public interface IRefundRequestValidator
{
    bool Validate(string orderId, decimal amount, out string? error);
}
