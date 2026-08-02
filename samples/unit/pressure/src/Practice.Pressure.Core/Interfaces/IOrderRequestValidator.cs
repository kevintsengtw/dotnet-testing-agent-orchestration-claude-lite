using Practice.Pressure.Core.Models;

namespace Practice.Pressure.Core.Interfaces;

public interface IOrderRequestValidator
{
    bool Validate(CustomerOrderRequest request, out string? error);
}
