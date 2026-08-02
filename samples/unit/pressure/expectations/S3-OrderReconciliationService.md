# S3 — OrderReconciliationService

不丟例外、以回傳值表達失敗語意；命名不一致；一個吞例外的方法；一個拼字錯誤但故意保留的方法名。

## 1. 必測行為清單

**建構子**
- `orderStore` 或 `auditLogger` 為 null → 拋 `ArgumentNullException`

**`GetOrderById(string orderId)`**
- `orderId` 為 null／空白 → 回傳 `null`（不呼叫 `_orderStore`）
- `orderId` 有效但 store 查無 → 回傳 `null`
- `orderId` 有效且查得到 → 回傳對應 `OrderRecord`

**`RetrieveOrderDetail(string orderId)`**
- store 查無 → 回傳 `null`
- 查得到 → 回傳 `OrderDetailSnapshot`，四個欄位與來源 `OrderRecord` 一致

**`FetchOrders(string customerId)`**
- store 正常回傳（含空集合）→ 原樣回傳
- **store 拋例外 → 回傳空集合，不重新拋出**（需用 NSubstitute `.Throws()` 主動觸發，才能命中 catch 區塊）

**`ReconcileOrders(IReadOnlyList<string> orderIds)`**（回傳值語意是本題核心）
- `orderIds` 為 null 或空集合 → `-1`
- `orderIds` 內含 null／空白字串 → `-1`
- 全部訂單核對失敗（查無／`Status == "Voided"`／`MarkReconciled` 回 false）→ `-1`
- 部分成功部分失敗（混合）→ `0`
- 全部成功 → 正數（等於清單筆數）
- 對每個失敗項目，需驗證 `_auditLogger.LogMismatch` 有被呼叫且理由正確（`"訂單不存在"` vs `"訂單已作廢"`）

**`CalculateDiscout(OrderRecord order, decimal customerLoyaltyScore)`**（方法名保留原拼字，不得改名）
- `order` 為 null → 拋 `ArgumentNullException`
- `customerLoyaltyScore < 0` → 原價（不打折）
- `0 <= customerLoyaltyScore < 50` → 原價（不打折）
- `50 <= customerLoyaltyScore < 100` → ×0.9
- `customerLoyaltyScore >= 100` → ×0.8

## 2. 已知陷阱

- **`GetOrderById` 的 `null` 回傳有兩種不同成因**：`orderId` 格式無效（未呼叫 store）與「格式有效但查無此單」（呼叫了 store，回傳 null）——兩者回傳值相同，容易被簡化成 1 個測試案例，但需要用 mock 的 `Received()`/`DidNotReceive()` 分別驗證是否有呼叫 store 才算測到位
- **`ReconcileOrders` 的 `-1` 有兩種不同成因**：「輸入驗證失敗」與「輸入合法但全部核對失敗」——共用同一個回傳值，這是刻意保留的歷史包袱（見程式碼註解），測試必須分別覆蓋這兩種成因，不能只測其中一種就宣稱涵蓋
- **`ReconcileOrders` 回傳 `0`（部分成功）時遺失確切成功筆數**：這是已知的資訊遺失，不是需要修的 bug；測試應驗證回傳值本身，不要嘗試從回傳值反推成功了幾筆
- **`CalculateDiscout` 的 `< 0` 與 `[0, 50)` 兩個不同區間回傳相同結果**（原價）：等價類邊界陷阱，容易被合併成一個測試案例而漏掉其中一個區間
- **`FetchOrders` 的例外分支容易被忽略**：正常路徑（含回傳空集合）看起來已經覆蓋「客戶沒有訂單」的情境，測試者容易誤以為不需要額外測「查詢本身失敗」的情境，但這是完全不同的程式碼路徑（try/catch 的 catch 區塊）
- **命名不一致本身不是缺陷**：`GetOrderById`／`FetchOrders`／`RetrieveOrderDetail` 三種動詞混用、`CalculateDiscout` 拼字錯誤，都是刻意保留的真實遺留程式碼特徵，Author 不應該「順手」重新命名或修正拼字

## 3. 合理的不可測項

無。S3 的所有分支都可由公開 API 觸發（`FetchOrders` 的例外分支需透過 mock 主動丟例外，但仍屬 testable，不算不可測）。

## 4. 預期 coverage

- Line／branch coverage 皆應可達 **100%**——沒有死碼或防禦性分支
- 若 Reviewer 回報任何 `uncoverable` 判定，需附具體理由重新檢視；本類別預期不應出現 uncoverable 項目
