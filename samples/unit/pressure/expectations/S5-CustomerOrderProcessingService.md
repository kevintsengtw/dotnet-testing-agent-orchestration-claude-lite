# S5 — CustomerOrderProcessingService（地獄難度）

13 個建構子依賴（3 repository＋4 外部服務＋2 驗證器＋cache＋config reader＋logger＋TimeProvider）；
8 個公開方法跨 4 個領域；7 個私有方法部分共用；**2 個私有可變欄位造成呼叫順序影響結果**——這是本題最關鍵的考點。

## 1. 必測行為清單

### 訂單建立

**`ProcessAsync(CustomerOrderRequest request)`**
- `request == null` → 拋 `ArgumentNullException`
- `request.OrderId` 與**上一次成功處理的訂單編號相同**（同一個服務實例）→ 失敗，`FailureReason` 含「重複送出」，且**不會**呼叫任何 repository／payment（在 `IsDuplicateOfLastProcessed` 就短路）
- `_orderRequestValidator.Validate` 回 false → 失敗，`FailureReason` 為「訂單驗證失敗」
- 庫存不足（`_inventoryRepository.GetStockAsync` < `Quantity`）→ 失敗「庫存不足」
- `_paymentProcessor.ChargeAsync` 回 false → 失敗「付款失敗」
- 全部成功 → `Success = true`，`TotalAmount` 依 `CalculateTotals` 規則計算，且需驗證：`_orderRepository.SaveAsync`、`_inventoryRepository.DecrementStockAsync`、`_customerNotifier.NotifyOrderCreatedAsync` 皆有被呼叫
- **成功後，同一實例再次呼叫 `ProcessAsync` 傳入相同 `OrderId`** → 第二次必須回傳「重複送出」失敗，即使所有 mock 都還是設定成功（見第 2 節順序相依）

**`ProcessExpressAsync(CustomerOrderRequest request)`**
- `request == null` → 拋 `ArgumentNullException`
- `EnableExpressCheckoutBypass = true` → 完全跳過 `_orderRequestValidator`（需用 `Received(0)`／`DidNotReceive()` 驗證未呼叫）
- `EnableExpressCheckoutBypass = false` → 驗證行為與 `ProcessAsync` 相同
- **本方法沒有重複送出檢查**：即使 `OrderId` 與上次 `ProcessAsync`／`ProcessExpressAsync` 處理過的相同，仍會正常處理（跟 `ProcessAsync` 的行為差異本身就是一個必測案例）
- 成功路徑不檢查庫存（`ProcessExpressAsync` 沒有呼叫 `CheckInventoryAvailability`）——需驗證 `_inventoryRepository.GetStockAsync` 未被呼叫

### 庫存調撥

**`AllocateInventoryAsync(string sku, int quantity)`**
- `sku` 空白或 `quantity <= 0` → 失敗「SKU 或數量無效」
- 庫存不足 → 失敗「可用庫存不足」
- `EnableInventoryReservationHold = true` → 呼叫 `_inventoryAllocationService.ReserveAsync`；失敗則回「預留庫存失敗」
- `EnableInventoryReservationHold = false` → 改呼叫 `_inventoryRepository.DecrementStockAsync`（**不**呼叫 `ReserveAsync`）
- 成功 → `RemainingStock` 來自 `_inventoryRepository.GetStockAsync` 的最新值

**`ReleaseInventoryAsync(string sku, int quantity)`**
- `sku` 空白或 `quantity <= 0` → 回 `false`，不呼叫任何依賴
- 有效輸入 → 回 `true`，`_inventoryAllocationService.ReleaseAsync` 與 `_inventoryRepository.IncrementStockAsync` 皆須呼叫

### 退款計算

**`CalculateRefundAsync(string orderId, decimal requestedAmount)`**
- `orderId` 空白或 `requestedAmount <= 0` → `NotEligible`「參數無效」
- 已退款總額＋本次調整後金額 > `requestedAmount * 2` → `NotEligible`「累計退款已超過合理上限」
- 正常情況 → `Eligible`，金額＝`requestedAmount * rate`（rate 來自 `GetCachedRate("USD")`，**固定字串 "USD"，與訂單原幣別無關**）

**`ProcessRefundAsync(string orderId, decimal requestedAmount)`**
- `orderId` 空白 → `false`
- `EnableRefundAutoApproval = true` 且 `requestedAmount <= 1000` → 跳過 `_refundRequestValidator`（自動核准）
- 其餘情況需經過 `_refundRequestValidator.Validate`；驗證失敗 → `false`，且 `_logger.Warn` 被呼叫
- `_paymentProcessor.RefundAsync` 回 false → `false`
- 成功 → `true`，`_refundRecordRepository.RecordRefundAsync`、`_customerNotifier.NotifyRefundProcessedAsync` 皆須呼叫；且會更新 `_lastProcessedId`（見第 2 節）

### 報表匯出

**`ExportMonthlySalesReportAsync(int year, int month)`**
- 目標年月晚於 `_timeProvider.GetUtcNow()` 所在月份 → 回傳全 0 報表，**不呼叫** `_orderRepository.GetMonthlySalesAsync`（需用 `DidNotReceive()` 驗證）
- 目標年月為當月或之前 → 呼叫 repository，回傳其結果組成的報表

**`ExportPendingOrderIdsAsync()`**
- 直接透傳 `_orderRepository.GetPendingOrderIdsAsync()` 的結果

## 2. 已知陷阱（本題最重要的部分）

### 陷阱 A：`_lastProcessedId` 造成的呼叫順序相依

`TrackLastProcessed` 在 `ProcessAsync`／`ProcessExpressAsync`／`ProcessRefundAsync` 成功後都會寫入 `_lastProcessedId`；`IsDuplicateOfLastProcessed` 只在 `ProcessAsync` 開頭讀取。這代表：

- 用**全新實例**呼叫 `ProcessAsync(orderX)` 會成功
- 但如果同一個實例**先前**已經成功處理過 `orderX`（不論是透過 `ProcessAsync` 還是 `ProcessExpressAsync`），再呼叫 `ProcessAsync(orderX)` 會被判定為重複送出而失敗

**這不是「同一個測試方法內連續呼叫兩次」才會踩到的邊角案例**——只要測試沒有為每個案例建立全新的 `CustomerOrderProcessingService` 實例（例如在 constructor/setup 共用同一個 sut 給多個 `[Theory]` case），就可能無意間讓某個案例的 `OrderId` 剛好等於前一個案例用過的值，導致測試結果不穩定。Reviewer 審查時要特別檢查測試是否每案例都重建 sut。

### 陷阱 B：`_cachedRates` 造成的跨方法呼叫順序相依

`GetCachedRate` 只在字典沒有該幣別時才呼叫 `_taxCalculationService.GetExchangeRateAsync`，之後永遠吃快取。`ProcessAsync`／`ProcessExpressAsync`（經由 `CalculateTotals`）與 `CalculateRefundAsync` **共用同一個 `_cachedRates` 字典**。

具體場景：同一實例先呼叫 `ProcessAsync`（幣別 USD，`GetExchangeRateAsync("USD")` 回 1.0）成功後，即使把 `_taxCalculationService.GetExchangeRateAsync("USD")` 的 stub 改成回傳 2.0，之後呼叫 `CalculateRefundAsync` 用的仍是快取住的 1.0，不會反映新的 stub 值。**單獨用一個全新實例呼叫 `CalculateRefundAsync` 會得到用 2.0 算出的結果，兩者不同**——這正是「先呼叫 A 再呼叫 B 的結果，與單獨呼叫 B 不同」的具體體現。

必測案例：同一實例、不同幣別呼叫順序組合，驗證第二次呼叫沒有重新打外部服務（`Received(1)` 而非 `Received(2)`），且金額計算確實用了快取值而非「假設重新查詢」的值。

### 其他陷阱

- **12-14 個依賴全部注入，但單一方法只用其中一部分**：容易被誤以為每個方法都要驗證全部 13 個依賴的互動；正確做法是只驗證該方法實際用到的子集，其餘用 `DidNotReceive()` 佐證「沒有被誤用」即可，不需要每個依賴都做完整互動驗證
- **`ProcessAsync` 與 `ProcessExpressAsync` 高度相似但行為不同**（重複送出檢查、庫存檢查有無）：容易把兩者的測試場景搞混或漏測其中一個的差異點
- **`CalculateRefundAsync` 固定用 `"USD"` 而非訂單原始幣別**：這是遺留程式碼的不一致，不是需要修的 bug，但如果訂單本身是非 USD 幣別，測試需要意識到退款試算跟訂單建立用的匯率鍵值不同
- **5 個 config 開關（`EnableTaxCalculation`／`UseNewPricingEngine`／`EnableExpressCheckoutBypass`／`EnableInventoryReservationHold`／`EnableRefundAutoApproval`）都需要兩個值都測到**；`IConfigurationReader.GetBool` 是通用介面，同一個 mock 對不同 key 要回傳不同值，測試需要用 `Returns` 依 key 參數區分，不能用同一個無條件 `Returns(true)`

## 3. 合理的不可測項

無明確的靜態不可達分支。但陷阱 A／B 描述的順序相依行為，若測試套件的每個測試案例都各自建立全新 `CustomerOrderProcessingService` 實例（一般 xUnit 慣例：constructor 每個測試方法都重跑一次），陷阱 A／B 所在的「重複」分支（`IsDuplicateOfLastProcessed` 回 true 那一支、`GetCachedRate` 命中快取那一支）就**不會自然被覆蓋**——這兩支必須靠**同一測試方法內、對同一個 sut 實例連續呼叫兩次**才能觸發，屬於「容易被規範（每案例一個新 sut）排除在外」的 testable 分支，不是不可達，需在 `missingTestCases` 中明確列出並用「同一實例連續呼叫」的方式補測。

## 4. 預期 coverage

- Line coverage：所有分支皆可由公開 API 觸發（含陷阱 A／B），完整測試下**理論上**應可達 100%，但實測值合理落在 **99%~100%**（見下一點例外）
- Branch coverage：同上，理論上可達 **100%**；若 Reviewer 回報 `IsDuplicateOfLastProcessed` 為 true 的分支或 `GetCachedRate` 命中快取的分支為 uncovered，這是**真實缺口**（陷阱 A／B 沒有被同一實例連續呼叫測到），必須列入 `missingTestCases`，不得判定為 uncoverable
- **例外（與陷阱設計無關的已知落差）**：`RefundAutoApprovalThreshold` 等 `private const` 欄位宣告，coverage 工具（coverlet／Cobertura）可能將該行標記為 uncovered line——這是 const 欄位在編譯期被內聯為字面值、不產生可執行 IL 的固有現象，不是測試缺口。Reviewer 判定為 `uncoverable` 且附具體理由（const 內聯、無可執行 IL）時應視為**正確判定**，不列入失敗案例；line coverage 因此略低於 100%（實測約 99.47%）屬正常
- 若最終 coverage 未達 100% 但報告中完全沒有提到「同一實例連續呼叫」或「呼叫順序」，且落差也不是上述 const 欄位例外，代表 Author／Reviewer 都沒抓到本題的核心考點，應視為本次壓測的失敗案例
