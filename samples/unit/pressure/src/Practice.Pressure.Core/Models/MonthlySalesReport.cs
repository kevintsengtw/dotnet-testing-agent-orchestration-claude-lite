namespace Practice.Pressure.Core.Models;

/// <summary>
/// 月度銷售報表
/// </summary>
public record MonthlySalesReport(int Year, int Month, decimal TotalRevenue, int OrderCount);
