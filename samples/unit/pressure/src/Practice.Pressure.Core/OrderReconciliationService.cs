using Practice.Pressure.Core.Interfaces;
using Practice.Pressure.Core.Models;

namespace Practice.Pressure.Core;

/// <summary>
/// 訂單核對服務。刻意不丟例外，用回傳值表達失敗——這是財務對帳流程的既有慣例，
/// 呼叫端（排程工具）不處理例外，只看回傳值決定要不要重跑。
/// </summary>
public class OrderReconciliationService
{
    private readonly IReconciliationOrderStore _orderStore;
    private readonly IReconciliationAuditLogger _auditLogger;

    public OrderReconciliationService(IReconciliationOrderStore orderStore, IReconciliationAuditLogger auditLogger)
    {
        _orderStore = orderStore ?? throw new ArgumentNullException(nameof(orderStore));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    /// <summary>
    /// 依訂單編號查詢。找不到或編號格式無效都回傳 null，不丟例外。
    /// </summary>
    public OrderRecord? GetOrderById(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return null;

        return _orderStore.FindById(orderId);
    }

    /// <summary>
    /// 舊版留下的查詢方法，語意跟 GetOrderById 幾乎重疊，但回傳型別不同，
    /// 有些舊呼叫端還在用，沒人敢合併。
    /// </summary>
    public OrderDetailSnapshot? RetrieveOrderDetail(string orderId)
    {
        var order = _orderStore.FindById(orderId);
        if (order is null)
            return null;

        return new OrderDetailSnapshot(order.OrderId, order.CustomerId, order.Amount, order.Status);
    }

    /// <summary>
    /// 依客戶查所有訂單。查詢來源異常時吞掉例外回傳空清單——
    /// 這樣呼叫端看到空清單無法分辨「真的沒有訂單」還是「查詢失敗」。
    /// </summary>
    public List<OrderRecord> FetchOrders(string customerId)
    {
        try
        {
            return _orderStore.FindByCustomer(customerId).ToList();
        }
        catch (Exception)
        {
            // TODO: 之前有加 log，後來 audit logger 介面改版忘記補回來
            // _auditLogger.LogMismatch(customerId, "查詢失敗");
            return new List<OrderRecord>();
        }
    }

    /// <summary>
    /// 批次核對訂單狀態。
    /// 回傳值：-1 = 驗證失敗（清單為空／含無效編號）或全部核對失敗；
    /// 0 = 部分核對成功（無法得知確切成功筆數，這是已知的資訊遺失）；
    /// 正數 = 全數核對成功的筆數。
    /// </summary>
    public int ReconcileOrders(IReadOnlyList<string> orderIds)
    {
        if (orderIds is null || orderIds.Count == 0)
            return -1;

        if (orderIds.Any(string.IsNullOrWhiteSpace))
            return -1;

        var successCount = 0;
        var failureCount = 0;

        foreach (var orderId in orderIds)
        {
            var order = _orderStore.FindById(orderId);
            if (order is null)
            {
                failureCount++;
                _auditLogger.LogMismatch(orderId, "訂單不存在");
                continue;
            }

            if (order.Status == "Voided")
            {
                failureCount++;
                _auditLogger.LogMismatch(orderId, "訂單已作廢");
                continue;
            }

            if (_orderStore.MarkReconciled(orderId))
            {
                successCount++;
            }
            else
            {
                failureCount++;
            }
        }

        if (successCount == 0)
            return -1;

        if (failureCount > 0)
            return 0;

        return successCount;
    }

    /// <summary>
    /// 拼字打錯的方法名（Discout 少一個 n），早期版本流出去了，改名會動到呼叫端，先留著。
    /// </summary>
    public decimal CalculateDiscout(OrderRecord order, decimal customerLoyaltyScore)
    {
        if (order is null)
            throw new ArgumentNullException(nameof(order));

        // 理論上 loyaltyScore 不會是負值，但呼叫端偶爾會傳錯，這裡不擋、直接視同無折扣處理
        if (customerLoyaltyScore < 0)
            return order.Amount;

        if (customerLoyaltyScore >= 100)
            return order.Amount * 0.8m;

        if (customerLoyaltyScore >= 50)
            return order.Amount * 0.9m;

        return order.Amount;
    }
}
