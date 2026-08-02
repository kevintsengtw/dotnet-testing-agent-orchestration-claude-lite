using Practice.Pressure.Core.Interfaces;
using Practice.Pressure.Core.Models;

namespace Practice.Pressure.Core;

/// <summary>
/// 客戶訂單處理服務。這是三個團隊（訂單、庫存、財務）共用的門面，
/// 歷史上每個團隊要加功能都直接掛在這個類別上，沒人有空拆。
/// </summary>
public class CustomerOrderProcessingService
{
    private readonly ICustomerOrderRepository _orderRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IRefundRecordRepository _refundRecordRepository;
    private readonly IPaymentProcessor _paymentProcessor;
    private readonly IInventoryAllocationService _inventoryAllocationService;
    private readonly ICustomerNotifier _customerNotifier;
    private readonly ITaxCalculationService _taxCalculationService;
    private readonly IOrderRequestValidator _orderRequestValidator;
    private readonly IRefundRequestValidator _refundRequestValidator;
    private readonly IPricingCache _pricingCache;
    private readonly IConfigurationReader _configurationReader;
    private readonly IAppLogger _logger;
    private readonly TimeProvider _timeProvider;

    private string? _lastProcessedId;
    private readonly Dictionary<string, decimal> _cachedRates = new();

    private const decimal RefundAutoApprovalThreshold = 1000m;

    public CustomerOrderProcessingService(
        ICustomerOrderRepository orderRepository,
        IInventoryRepository inventoryRepository,
        IRefundRecordRepository refundRecordRepository,
        IPaymentProcessor paymentProcessor,
        IInventoryAllocationService inventoryAllocationService,
        ICustomerNotifier customerNotifier,
        ITaxCalculationService taxCalculationService,
        IOrderRequestValidator orderRequestValidator,
        IRefundRequestValidator refundRequestValidator,
        IPricingCache pricingCache,
        IConfigurationReader configurationReader,
        IAppLogger logger,
        TimeProvider timeProvider)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _refundRecordRepository = refundRecordRepository ?? throw new ArgumentNullException(nameof(refundRecordRepository));
        _paymentProcessor = paymentProcessor ?? throw new ArgumentNullException(nameof(paymentProcessor));
        _inventoryAllocationService = inventoryAllocationService ?? throw new ArgumentNullException(nameof(inventoryAllocationService));
        _customerNotifier = customerNotifier ?? throw new ArgumentNullException(nameof(customerNotifier));
        _taxCalculationService = taxCalculationService ?? throw new ArgumentNullException(nameof(taxCalculationService));
        _orderRequestValidator = orderRequestValidator ?? throw new ArgumentNullException(nameof(orderRequestValidator));
        _refundRequestValidator = refundRequestValidator ?? throw new ArgumentNullException(nameof(refundRequestValidator));
        _pricingCache = pricingCache ?? throw new ArgumentNullException(nameof(pricingCache));
        _configurationReader = configurationReader ?? throw new ArgumentNullException(nameof(configurationReader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    // ===== 訂單建立 =====

    public async Task<OrderCreationResult> ProcessAsync(CustomerOrderRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (IsDuplicateOfLastProcessed(request.OrderId))
        {
            return OrderCreationResult.Failed("重複送出，與上一筆處理的訂單編號相同");
        }

        if (!await ValidateInternal(request))
        {
            return OrderCreationResult.Failed("訂單驗證失敗");
        }

        if (!await CheckInventoryAvailability(request.Sku, request.Quantity))
        {
            return OrderCreationResult.Failed("庫存不足");
        }

        var total = await CalculateTotals(request);

        if (!await _paymentProcessor.ChargeAsync(request.CustomerId, total))
        {
            return OrderCreationResult.Failed("付款失敗");
        }

        await _orderRepository.SaveAsync(request, total);
        await _inventoryRepository.DecrementStockAsync(request.Sku, request.Quantity);

        TrackLastProcessed(request.OrderId);
        await LogAndNotifyAsync(request.CustomerId, request.OrderId, $"訂單 {request.OrderId} 建立成功");

        return OrderCreationResult.Succeeded(request.OrderId, total);
    }

    // 快速結帳：VIP 專用通道，可用 EnableExpressCheckoutBypass 開關跳過完整驗證
    public async Task<OrderCreationResult> ProcessExpressAsync(CustomerOrderRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var bypassValidation = _configurationReader.GetBool("EnableExpressCheckoutBypass", false);

        if (!bypassValidation && !await ValidateInternal(request))
        {
            return OrderCreationResult.Failed("訂單驗證失敗");
        }

        var total = await CalculateTotals(request);

        if (!await _paymentProcessor.ChargeAsync(request.CustomerId, total))
        {
            return OrderCreationResult.Failed("付款失敗");
        }

        await _orderRepository.SaveAsync(request, total);
        TrackLastProcessed(request.OrderId);
        await LogAndNotifyAsync(request.CustomerId, request.OrderId, $"訂單 {request.OrderId}（快速結帳）建立成功");

        return OrderCreationResult.Succeeded(request.OrderId, total);
    }

    // ===== 庫存調撥 =====

    public async Task<InventoryAllocationResult> AllocateInventoryAsync(string sku, int quantity)
    {
        if (string.IsNullOrWhiteSpace(sku) || quantity <= 0)
        {
            return InventoryAllocationResult.Failed("SKU 或數量無效");
        }

        if (!await CheckInventoryAvailability(sku, quantity))
        {
            return InventoryAllocationResult.Failed("可用庫存不足");
        }

        var useHold = _configurationReader.GetBool("EnableInventoryReservationHold", false);

        if (useHold)
        {
            if (!await _inventoryAllocationService.ReserveAsync(sku, quantity))
            {
                return InventoryAllocationResult.Failed("預留庫存失敗");
            }
        }
        else
        {
            await _inventoryRepository.DecrementStockAsync(sku, quantity);
        }

        var remaining = await _inventoryRepository.GetStockAsync(sku);
        return InventoryAllocationResult.Succeeded(remaining);
    }

    public async Task<bool> ReleaseInventoryAsync(string sku, int quantity)
    {
        if (string.IsNullOrWhiteSpace(sku) || quantity <= 0)
            return false;

        await _inventoryAllocationService.ReleaseAsync(sku, quantity);
        await _inventoryRepository.IncrementStockAsync(sku, quantity);
        return true;
    }

    // ===== 退款計算 =====

    public async Task<RefundCalculationResult> CalculateRefundAsync(string orderId, decimal requestedAmount)
    {
        if (string.IsNullOrWhiteSpace(orderId) || requestedAmount <= 0)
        {
            return RefundCalculationResult.NotEligible("參數無效");
        }

        var alreadyRefunded = await _refundRecordRepository.GetTotalRefundedAsync(orderId);

        // TODO(2019): 之後應該用訂單原幣別換算，先用 USD 頂著
        var rate = await GetCachedRate("USD");
        var adjustedAmount = requestedAmount * rate;

        if (alreadyRefunded + adjustedAmount > requestedAmount * 2)
        {
            return RefundCalculationResult.NotEligible("累計退款已超過合理上限");
        }

        return RefundCalculationResult.Eligible(adjustedAmount);
    }

    public async Task<bool> ProcessRefundAsync(string orderId, decimal requestedAmount)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return false;

        var autoApprove = _configurationReader.GetBool("EnableRefundAutoApproval", false)
            && requestedAmount <= RefundAutoApprovalThreshold;

        if (!autoApprove)
        {
            var valid = _refundRequestValidator.Validate(orderId, requestedAmount, out var error);
            if (!valid)
            {
                _logger.Warn($"退款驗證失敗：{error}");
                return false;
            }
        }

        if (!await _paymentProcessor.RefundAsync(orderId, requestedAmount))
            return false;

        await _refundRecordRepository.RecordRefundAsync(orderId, requestedAmount);
        TrackLastProcessed(orderId);

        _logger.Info($"訂單 {orderId} 退款 {requestedAmount:C} 已處理");
        // MVP 版本先用 orderId 充當通知對象，之後應該查訂單拿真正的 customerId
        await _customerNotifier.NotifyRefundProcessedAsync(orderId, orderId, requestedAmount);

        return true;
    }

    // ===== 報表匯出 =====

    public async Task<MonthlySalesReport> ExportMonthlySalesReportAsync(int year, int month)
    {
        var now = _timeProvider.GetUtcNow();
        if (new DateTime(year, month, 1) > new DateTime(now.Year, now.Month, 1))
        {
            return new MonthlySalesReport(year, month, 0m, 0);
        }

        var (orderCount, totalRevenue) = await _orderRepository.GetMonthlySalesAsync(year, month);
        return new MonthlySalesReport(year, month, totalRevenue, orderCount);
    }

    public async Task<IReadOnlyList<string>> ExportPendingOrderIdsAsync()
    {
        return await _orderRepository.GetPendingOrderIdsAsync();
    }

    // ===== private 共用邏輯 =====

    private async Task<bool> ValidateInternal(CustomerOrderRequest request)
    {
        var basicValid = _orderRequestValidator.Validate(request, out var error);
        if (!basicValid)
        {
            _logger.Warn($"訂單驗證失敗：{error}");
        }

        return await Task.FromResult(basicValid);
    }

    private async Task<decimal> CalculateTotals(CustomerOrderRequest request)
    {
        if (!_pricingCache.TryGetPrice(request.Sku, out var unitPrice))
        {
            unitPrice = request.UnitPrice;
            _pricingCache.SetPrice(request.Sku, unitPrice);
        }

        var subtotal = unitPrice * request.Quantity;

        var enableTax = _configurationReader.GetBool("EnableTaxCalculation", true);
        var tax = enableTax
            ? await _taxCalculationService.CalculateTaxAsync(subtotal, request.CurrencyCode)
            : 0m;

        var rate = await GetCachedRate(request.CurrencyCode);
        var useNewEngine = _configurationReader.GetBool("UseNewPricingEngine", false);

        return useNewEngine
            ? Math.Round((subtotal + tax) * rate, 2, MidpointRounding.AwayFromZero)
            : Math.Round((subtotal + tax) * rate, 2);
    }

    // 匯率快取：同一個實例的同一個幣別只打一次外部匯率服務，之後都吃快取——效能考量
    private async Task<decimal> GetCachedRate(string currencyCode)
    {
        if (_cachedRates.TryGetValue(currencyCode, out var cached))
        {
            return cached;
        }

        var rate = await _taxCalculationService.GetExchangeRateAsync(currencyCode);
        _cachedRates[currencyCode] = rate;
        return rate;
    }

    private async Task<bool> CheckInventoryAvailability(string sku, int quantity)
    {
        var stock = await _inventoryRepository.GetStockAsync(sku);
        return stock >= quantity;
    }

    private void TrackLastProcessed(string orderId)
    {
        _lastProcessedId = orderId;
    }

    private bool IsDuplicateOfLastProcessed(string orderId)
    {
        return _lastProcessedId is not null && _lastProcessedId == orderId;
    }

    private async Task LogAndNotifyAsync(string customerId, string orderId, string message)
    {
        _logger.Info(message);
        await _customerNotifier.NotifyOrderCreatedAsync(customerId, orderId);
    }
}
