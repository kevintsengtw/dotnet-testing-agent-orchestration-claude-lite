# S2 — LegacyMemberProfileService（難）

考混雜依賴＋靜態呼叫。建構子注入 2 個介面（`IMemberRepository`／`IMemberNotifier`）＋ 1 個無介面的具體類別（`MemberDisplayNameFormatter`，`sealed`、無 `virtual` 成員）；方法內同時呼叫靜態工具 `MemberHelper`（門檻表＋地區對照表寫死）與 `DateTime.Now`（legacy 特徵，沒有 `TimeProvider`）。6 個公開方法，依賴組合各不相同。

## 1. 必測行為清單

**建構子**
- `repository`／`notifier`／`formatter` 為 null → 各自拋 `ArgumentNullException`

**`GetDisplayProfile(string memberId)`**（介面＋具體類別＋靜態，三方混用）
- 查無會員 → 回傳 `null`
- 查得到 → `DisplayName` 經 `_formatter.Format(...)` 組成、`Tier` 經 `MemberHelper.ComputeTier(...)` 算出、`RegionDisplayName` 經 `MemberHelper.GetRegionDisplayName(...)` 查出，三者皆須驗證正確組合成 `MemberProfileSummary`

**`GetCurrentTier(string memberId)`**（介面＋靜態）
- 查無會員 → 回傳 `null`
- 消費金額門檻邊界（`MemberHelper` 內部門檻：`SilverThreshold = 10000m`、`GoldThreshold = 50000m`）：
  - `9999.99` → `Standard`
  - `10000` → `Silver`（起始邊界含）
  - `49999.99` → `Silver`
  - `50000` → `Gold`（起始邊界含）

**`FormatDisplayName(string firstName, string lastName)`**（只依賴具體類別，不經介面、不經靜態）
- 姓名皆空白 → `"未知會員"`
- 只有姓為空白 → 回傳去除前後空白後的名
- 只有名為空白 → 回傳去除前後空白後的姓
- 皆有值 → `"{姓} {名}"`（姓在前），且前後空白需被裁剪

**`GetRegionDisplayName(string regionCode)`**（唯一完全不用注入依賴、只走靜態路徑的方法）
- `"TW"` → `"台灣"`、`"JP"` → `"日本"`、`"US"` → `"美國"`、`"CN"` → `"中國"`
- 未知代碼（如 `"FR"`、空字串、null）→ `"未知地區"`

**`GetAccountAgeDays(string memberId)`**（介面＋`DateTime.Now`，無法注入假時鐘）
- 查無會員 → 回傳 `-1`
- 查得到 → 回傳 `(DateTime.Now - profile.JoinedAt).TotalDays` 轉 `int`；測試必須用「相對值」或「容忍範圍」驗證（例如 `JoinedAt = DateTime.Now.AddDays(-30)` 後斷言結果約為 30，或斷言落在合理區間），不能寫死絕對天數

**`NotifyIfEligibleForUpgrade(string memberId)`**（介面×2＋靜態＋`DateTime.Now`，四種依賴全混用）
- 查無會員 → 回傳 `false`，`_notifier` 不應被呼叫
- 消費金額算出的 `computedTier <= RecordedTier`（含相等）→ 回傳 `false`，`_notifier` 不應被呼叫
- `computedTier > RecordedTier` → 回傳 `true`，`_notifier.NotifyTierUpgrade(memberId, computedTier, ~DateTime.Now)` 被呼叫恰好一次；第三個時間參數同樣只能用容忍範圍驗證

## 2. 已知陷阱

- **`targetType` 判定容易兩難**：這個類別同時具備典型 service 特徵（介面注入、可測試設計）與 legacy 特徵（靜態呼叫、`DateTime.Now`、無介面具體類別）。不管判成 `service` 還是 `legacy`，重點是**判定依據要合理陳述**（例如「多數依賴走介面注入，靜態呼叫與具體類別是局部例外」），而不是只看有沒有介面就一刀切
- **具體類別依賴無法被 Mock，且是刻意設計成無法 Mock**：`MemberDisplayNameFormatter` 是 `sealed class`、沒有任何 `virtual`／`abstract` 成員，NSubstitute 無法對它建立 substitute（`Substitute.For<MemberDisplayNameFormatter>()` 會在執行期失敗，因為沒有可覆寫的成員可以攔截）。正確做法是直接 `new MemberDisplayNameFormatter()` 注入建構子，把它當成穩定、確定性的「測試邊界內」真實物件，不是需要隔離的外部依賴
- **靜態呼叫的門檻與對照表必須用實際數值**：`MemberHelper.ComputeTier`／`GetRegionDisplayName` 都是 `static`，測試無法替換或攔截它們，只能依賴真實的門檻值（10000／50000）與對照表（TW/JP/US/CN 四筆＋預設值）。Characterization 測試命名如果寫成「高消費會員應為金卡」而不帶出實際數字（如「消費滿 50000 應為金卡」），代表沒有真正核對過寫死的資料，只是照著方法名稱猜語意
- **`DateTime.Now` 兩處用法無法用 `FakeTimeProvider` 精確控制**：這個類別完全沒有注入 `TimeProvider`（刻意設計，legacy 特徵），`GetAccountAgeDays` 與 `NotifyIfEligibleForUpgrade` 涉及時間的斷言只能用相對值或容忍區間，不能像其他樣本一樣凍結時間後精確比對。如果測試作者試圖注入 `FakeTimeProvider`（例如誤以為建構子有這個依賴），會直接編譯不過——這本身就是一個訊號，代表沒有先讀懂建構子簽章
- **白名單排除 `private-internal-testing`，不該用 reflection**：`MemberHelper` 的兩個門檻常數是 `private`，不應該用反射直接讀取常數值來斷言，也不該對 `sealed` 的 `MemberDisplayNameFormatter` 用反射繞過無法 Mock 的限制——所有驗證都應該只透過 `LegacyMemberProfileService` 的公開方法觀察行為結果

## 3. 合理的不可測項

`LegacyMemberProfileService.cs` 本身沒有 `const` 欄位或結構性不可達分支，**不預期出現任何 uncoverable 項目**。

**注意**：`MemberHelper.cs` 內有 2 個 `private const decimal` 欄位宣告（`SilverThreshold`／`GoldThreshold`）。依 S1／S5／S4 已驗證的機制，`const decimal` 會被 coverage 工具計入分母但 0 命中，理論上屬於 uncoverable——**但這不影響 `LegacyMemberProfileService` 的 coverage 報告**，因為 `coverage-summary.mjs` 是以 `--target-source` 指定的單一檔案為準，`MemberHelper.cs` 是不同檔案，不會被算進「目標類別」的統計裡。這一點如果 Reviewer 報告誤把 `MemberHelper.cs` 的內容也算進 `LegacyMemberProfileService` 的 coverage（例如引用了 `MemberHelper.cs` 裡的行號），代表對 coverage 量測範圍的理解有誤，需要特別標記。

## 4. 預期 coverage

- Line／branch coverage（目標類別 `LegacyMemberProfileService`）：**理論上可達 100%**——這個檔案本身沒有 `const` 欄位、沒有防禦性分支、沒有結構性不可達路徑，6 個方法的所有分支都能由公開 API 組合觸發
- 若 coverage 未達 100%，落差應該是 testable（真正漏測，例如漏了某個門檻邊界或某個地區代碼），不應該出現任何 uncoverable 判定
- 承第 3 節：如果 Reviewer 報告的 uncoverable 清單提到 `MemberHelper.cs` 的常數宣告，代表混淆了「目標類別本身」與「目標類別依賴的其他檔案」，這是判定範圍錯誤，即使理由（const decimal 內聯）本身是對的
