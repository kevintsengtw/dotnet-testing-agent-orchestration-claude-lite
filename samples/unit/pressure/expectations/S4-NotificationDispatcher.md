# S4 — NotificationDispatcher（極難）

`DispatchAsync(NotificationRequest request, CancellationToken cancellationToken)`，考非同步＋時間＋副作用交織。單一公開方法，難度在深度而非廣度。

## 1. 必測行為清單

**Null guard**
- `request == null` → 拋 `ArgumentNullException`
- 建構子 4 個依賴（`pushChannel`／`emailChannel`／`auditRecorder`／`timeProvider`）為 null → 各自拋 `ArgumentNullException`

**靜音時段判定（`IsWithinQuietHours`，跨午夜區間，22:00~07:00）**
- 21:59 → 非靜音
- 22:00 → 靜音（起始邊界含）
- 23:30 → 靜音（跨午夜中段）
- 03:00 → 靜音（跨午夜後段）
- 06:59 → 靜音（結束邊界前）
- 07:00 → 非靜音（結束邊界不含）
- 14:00（正午附近）→ 非靜音，作為對照組

**Urgent 略過靜音**
- 靜音時段＋`IsUrgent = true` → Push 通道**照樣**被呼叫（不受靜音限制）

**Push 被靜音抑制時的行為**
- 靜音時段＋`IsUrgent = false`＋Email 首次即成功 → `Succeeded = true`、`FailedChannels` 為空、`AttemptCount = 1`；Push 通道**完全不會被呼叫**（`DidNotReceive()`）
- 上述情境但 Email 前幾次失敗、之後重試成功 → `AttemptCount` 對應實際重試次數，Push 全程仍不被呼叫

**正常派送（非靜音）**
- 兩通道皆首次成功 → `Succeeded = true`、`FailedChannels` 為空、`AttemptCount = 1`

**關鍵陷阱：部分通道失敗仍整體成功**
- 非靜音＋Push 連續 3 次（1 次初次＋2 次重試）皆失敗＋Email 首次成功 → `Succeeded = true`（**不是** `false`，也**不應該拋例外**）、`FailedChannels = ["Push"]`、`AttemptCount = 3`；需驗證 Push 通道被呼叫 3 次、Email 通道只被呼叫 1 次（成功後不再重試）

**全部失敗**
- 非靜音＋兩通道皆連續 3 次失敗 → `Succeeded = false`、`FailedChannels = ["Push", "Email"]`、`AttemptCount = 3`

**通道逾時 vs 呼叫端主動取消**
- 通道拋 `OperationCanceledException`，但傳入的 `cancellationToken` 本身**未被取消** → 視為該次失敗，進入重試邏輯，`DispatchAsync` 不會把例外往外拋
- 呼叫端傳入的 `cancellationToken` 本身**已被取消**（或在 `Task.Delay` 等待期間被取消）→ `DispatchAsync` 應該把 `OperationCanceledException` 往外拋，且**不應該**呼叫 `_auditRecorder.Record`
- ⚠️ **評分時的權重註記**：`InvokeChannelAsync` 上方保留了一條解釋這個 `catch...when` 過濾條件的註解（用來區分逾時與主動取消），這是**已文件化行為**。若工作流程測到這個區分，只能算「依文件寫測試」，**不計為「自行推導」的證據**——沿用 S5 兩輪驗證建立的區分（洩露/文件化的線索只能讓結果趨近文件水準，不可能超過；只有超出文件範圍的發現才是自行推導的決定性證據）。真正沒有文件提示、需要自行推導才能測到的是「靜音時段判定只在方法一進來時算一次」與「重試迴圈跨越靜音邊界時抑制狀態不變」這兩點——原始碼完全沒有註解提及。

**稽核記錄內容精確度（第二個陷阱：兩種結果寫入內容不同）**
- 全部失敗時：`Record` 傳入 `succeeded=false`、`failedChannels` 含兩個通道名稱、`attemptCount=3`
- 部分成功時：`Record` 傳入 `succeeded=true`、`failedChannels` 只含失敗的那個通道、`attemptCount` 對應實際重試次數
- 首次即完全成功時：`Record` 傳入 `succeeded=true`、`failedChannels` 為空、`attemptCount=1`
- 以上三種情況的**參數必須逐一斷言**，不能只驗證 `Record` 被呼叫過一次就算數

**重試間隔必須走注入的 `TimeProvider`**
- 測試需以 `FakeTimeProvider` 驅動重試（`Advance()` 推進而非真的等待 30 秒），且測試本身應能快速完成；若測試因為真的等了 30 秒（或整個掛住不動）才通過，代表 production code 沒有正確使用 `Task.Delay(TimeSpan, TimeProvider, CancellationToken)` 這個多載，這是本題除了兩個業務陷阱外，另一個「潛在但非故意」的技術正確性檢核點

## 2. 已知陷阱

- **「部分成功仍算成功」最容易被誤判**：測試作者或審查者可能直覺認為「有通道失敗」應該回傳 `Succeeded = false` 甚至讓方法拋例外，但這是既有的業務判斷（多通道只要有一個送達即可）。場景推導或審查時若把這條路徑寫成「應該失敗」或「應該拋例外」，就是誤判
- **「Push 被抑制」與「Push 失敗」必須分開**：兩者都會讓 `pushOk` 停在 `false`，但只有真正呼叫過且失敗的才會被加進 `FailedChannels`；判斷方式是看 `_pushChannel` 有沒有被呼叫過（`Received()`/`DidNotReceive()`），不能只看 `FailedChannels` 或 `Succeeded` 的值反推
- **靜音時段判定只在方法一進來時算一次**：重試迴圈中即使 `TimeProvider` 被 `FakeTimeProvider.Advance()` 推進跨過了靜音邊界（例如從 23:50 推進到隔天 07:10），Push 通道的抑制狀態**不會**中途改變——這需要一個專門的測試場景（時間在重試過程中跨越邊界）來鎖定，而不是只在方法呼叫當下測邊界值
- **通道逾時的例外分類**：`OperationCanceledException` 本身不足以判斷該不該重試，必須看「是不是呼叫端自己的 `cancellationToken` 被取消」——如果只做 `catch (OperationCanceledException)` 不分青紅皂白地吞掉，會讓呼叫端主動取消的請求也被誤判成單次通道失敗而繼續重試，這是本題最容易漏掉的例外處理細節
- **`Received(N)` 次數驗證比「有沒有被呼叫」更重要**：關鍵陷阱場景要求驗證 Push 通道被呼叫恰好 3 次（不多不少）、Email 通道只被呼叫 1 次（成功後不再打）——只驗證 `Received()`（至少一次）不足以證明重試邏輯正確在「已成功的通道不重試、未成功的通道才重試」這件事上運作正常

## 3. 合理的不可測項

原則上沒有結構性不可達的分支（不像 S1 有因前置條件互斥而恆假的判斷式）。唯一預期會出現的 uncoverable 是**編譯期產物**，不是邏輯陷阱：

- `MaxRetryCount`、`QuietHoursStart`、`QuietHoursEnd` 三個 `private const int` 欄位宣告（第 16、22、23 行）：與 S5、S1 已重現兩次的現象相同，const 欄位在編譯期被內聯為字面值，不產生可執行 IL，任何測試都無法讓這幾行被覆蓋率工具標記為已執行，屬於**第三次獨立樣本**出現同一種工具產物
- **對照組**：`RetryDelay`（第 19 行，`private static readonly TimeSpan`）**不是** `const`，是透過型別初始化建構子（static constructor / cctor）在執行期賦值，理論上會在任何測試觸發該類別第一次使用時執行到，**不應該**出現在 uncovered 清單裡。如果 Reviewer 把這一行也判定為 uncoverable，代表誤把 `static readonly` 當成 `const` 處理，是判定錯誤，需要修正

## 4. 預期 coverage

- Line coverage：扣除 3 行 const 欄位宣告（工具產物，非邏輯缺口），**理論上限低於 100%**，實測值預期落在 **95%~98%** 之間（總行數約 60~70 行可執行程式碼，3 行 const 佔比約 4~5%）
- Branch coverage：本題沒有設計任何結構性不可達分支，**理論上可達 100%**；若有分支 uncovered，必須是 testable（真正漏測），不應該有任何 uncoverable 判定套用在分支上（uncoverable 只該出現在上述 3 行 const 宣告，且是 `line` 類型而非 `branch` 類型）
- 若 Reviewer 回報 branch coverage 未達 100% 卻沒有列進 `missingTestCases`，或者把 `RetryDelay` 那一行也判定為 uncoverable，都代表判定有誤，需要回頭檢查
- 若最終結果完全沒有測試涉及「重試迴圈中跨越靜音邊界」或「呼叫端主動取消 vs 通道逾時」這兩類場景，即使 coverage 數字好看，也應視為本題核心陷阱沒有被完整驗證到
