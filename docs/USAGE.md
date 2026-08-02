# 使用指南

面向「已經裝好、要開始用」的讀者。部署步驟見 [docs/DEPLOYMENT.md](DEPLOYMENT.md)。

---

## 怎麼呼叫

Skill 名稱：`dotnet-testing-lite-orchestrator-unit`。用自然語言觸發即可，例如：

```text
請為 ProductService 撰寫單元測試
被測試目標：src/Services/ProductService.cs
測試專案：tests/MyProject.Tests/MyProject.Tests.csproj
```

**必要輸入**兩項：

1. **被測試目標檔案路徑**：單一 `.cs` 檔案
2. **測試專案路徑**：目標測試專案的 `.csproj`

兩者皆為路徑，不需要（也不應該）貼上程式碼內容。Orchestrator 不會先讀原始碼，會直接把路徑交給 Author，由 Author 自行讀取。

**範圍過濾**（只測某個方法）：在請求中說明即可，例如「只測試 `ProductService` 的 `CalculatePrice` 方法」。這屬於範圍過濾，不是多目標，工作流程會照常執行、把過濾條件原樣交給 Author。

**多類別請求會被拒絕**：一次只能處理一個類別。如果請求包含多個類別名稱或路徑（如「幫 Services 資料夾下所有類別寫測試」），Orchestrator 不會啟動任何 agent，會列出偵測到的目標並建議逐次執行的順序，需要你分次下指令。

---

## 流程與預期耗時

兩個階段循序執行，中間不可平行：

1. **Author**：讀目標原始碼與相依介面、推導測試場景並落檔（`scenarios.json`）、撰寫測試、執行 `dotnet build`／`dotnet test` 並在需要時修正（上限 3 輪）
2. **Reviewer**：獨立載入審查技能、自己執行一次 `dotnet test --collect:"XPlat Code Coverage"`（不採信 Author 宣稱）、量測目標類別 coverage、逐項審查並產出報告

**實測耗時一般落在 5～12 分鐘**（Author 佔多數、Reviewer 約 2～3 分鐘），依目標複雜度浮動：

| 目標複雜度 | 實測範圍 | 來源 |
|---|---|---|
| 簡單（純函式、無依賴） | ~5 分鐘 | 冒煙測試實測（`TemperatureConverter`，Author+Reviewer 合計 4 分 54 秒） |
| 中等（1～6 個依賴） | 約 8～9 分鐘 | 壓力驗證實測（S1 9:10、S2 8:38、S3 8:41，皆為 headless session 起訖時間） |
| 複雜（非同步＋時間控制，或 10+ 個依賴） | 約 11～15 分鐘 | 壓力驗證實測（S4 11:05；S5 兩輪 13:47／14:42，S5 建構子注入 13 個依賴） |

首次修正迴圈（`fixRounds > 0`）或大型測試檔分批寫入會拉長耗時；上述樣本 `fixRounds` 皆為 0，代表這是「一次到位」情境下的耗時，需要修正迴圈時會更長。

---

## 怎麼讀 Reviewer 報告

### 評分等級（`overallScore`）

| 分數 | 條件 |
|------|------|
| A+ | 零 issues，覆蓋完整（含無 testable coverage 缺口），全面符合 Skills 規範 |
| A | 僅 suggestion 級，覆蓋完整 |
| B+ | 少量 warning，缺 1～2 個邊界案例 |
| B | 多個 warning 或缺部分測試案例 |
| C+ | 有 error 級但整體結構尚可 |
| C | 多個 error，系統性問題 |
| D | 嚴重品質問題，建議重寫 |

coverage 數字不直接映射分數，但有兩條例外規則：testable 缺口沒有列進 `missingTestCases` 屬於報告本身不完整（不應該發生）；執行結果與 Author 宣稱不一致（`integrity` 級 error）時，分數上限鎖在 C+。

### `issues` 的三個嚴重度

| 嚴重度 | 定義 | 該怎麼處理 |
|--------|------|--------|
| `error` | 違反核心原則（一測多行為、Mock 具體類別、名稱與斷言矛盾、宣稱與實際不符、場景缺對應測試） | 必須修正 |
| `warning` | 偏離最佳實踐（命名模糊、斷言不精確、場景推導疏漏、巢狀規則缺覆蓋） | 建議修正 |
| `suggestion` | 可改善但不迫切（簡化斷言、冗餘展開檢視） | 可選，不套用也不影響正確性 |

### coverage 缺口：`testable` 與 `uncoverable`

Reviewer 對每一個 uncovered line／branch 都要逐項判定：

- **`testable`**：可以透過公開 API 觸發，只是還沒被測到，這種缺口**必須**列進 `missingTestCases`，漏列視為報告本身的缺陷
- **`uncoverable`**：Reviewer 主張這段程式碼無法從公開 API 觸發，**必須附具體理由**，說清楚「為什麼」（例如：某個條件被外層短路、某段是防禦性的 `default` case、某個常數宣告在編譯期已內聯不產生可執行 IL）

**為什麼 `uncoverable` 必須附反證**：這是 coverage 誠實性的底線，防止 Reviewer 用「不可測」當藉口迴避真正的漏測。特別是複合布林條件（`&&`／`||`）出現在 `When`／`Unless` 這類短路語法裡時，**預設視為 testable**，需要逐一運算元說明短路機制才能改判成 uncoverable，籠統寫「這裡測不到」是不合格的判定。

看到報告時，如果 `uncoverable` 項目的 `reason` 欄位只是重複描述現象而沒有解釋因果（例如只寫「防禦性程式碼」而不說明為什麼公開 API 無法觸發），這份報告的可信度就該打折扣。

---

## 修改流程怎麼觸發

Reviewer 產出報告後，工作流程**不會自動修改任何東西**，會停在原地等待你的指示。如果你同意套用 Reviewer 的建議，直接說「套用建議」或具體說明要修哪幾條即可，會依序觸發：

1. **Author（modification 模式）**：帶著 Reviewer 的 `issues` 與 `missingTestCases` 回頭修改測試檔
2. **Reviewer（re-review 模式）**：只針對「前次 issues 是否正確套用」與「新增測試品質」重新審查，**不會**主動發掘前次沒提過的新問題（避免「修改→再發現→再修改」的無限迴圈）

修改完成後一樣會呈現測試數變化、套用了哪些建議、重新評分、coverage 前後對比，並重貼一次 token 用量報表（同一份計量累計，不會重算）。

---

## 適用範圍與限制

如實列出，不誇大工作流程能力：

- **一次一個類別**：多類別請求會被拒絕（見上方「怎麼呼叫」），沒有平行處理選項
- **僅 xUnit**：測試框架固定為 xUnit，不支援其他框架
- **僅單元測試**：不涵蓋 integration／aspire／tunit 測試工作流程（那是原版才有的範圍，見 [README.md](../README.md)「何時該用原版」）
- **production code 需要「可測」的設計**：
  - 依賴真實 I/O、真實時鐘（`DateTime.Now`）、真實隨機數的程式碼，工作流程無法讓它們變得可控，只能退而求其次，用 Characterization Test（記錄現有行為）搭配相對值或容忍範圍斷言，測試精確度與穩定性都會打折扣
  - 大量使用靜態方法呼叫的程式碼，工作流程會把它判定為 `legacy` 而非 `service`，同樣只能用 Characterization Test 思維測「現有行為」，無法替換或攔截靜態呼叫本身
  - 沒有介面、無 `virtual`／`abstract` 成員的具體類別依賴無法被 Mock，測試必須直接建構真實物件。這在依賴本身是純資料轉換這類確定性邏輯時沒問題，但如果具體類別本身有外部副作用（如直接寫檔），會變成測試的隱性負擔

---

## 讓工作流程更有效的實務建議

以下建議是壓力驗證過程中的副產品，不是理論推測，皆有實測依據：

**1. Production code 的註解品質會直接影響 token 成本**

同一個類別（`NotificationDispatcher`），把原始碼裡三處「解釋測試陷阱後果」的註解拿掉、只留純業務脈絡後，Author 需要多花約 **29% token**、多寫 8 個測試場景才能達到跟原本相同的行為涵蓋度（壓力驗證 S5 兩輪對照資料）。反過來說：**寫清楚業務意圖、設計決策脈絡的註解，能讓 Author 少走推導的彎路**，這不是要你為了省 token 刻意加註解，而是良好註解本來就該做的事，省 token 只是附帶效果。

**2. 依賴走介面比具體類別好測**

沒有介面、`sealed` 或無 `virtual` 成員的具體類別依賴無法被 Mock，工作流程只能直接建構真實物件（壓力驗證 S2 驗證）。如果這個依賴本身邏輯單純（純資料轉換）沒問題；但如果它有外部副作用，會讓測試被迫承擔這個副作用，難以隔離。

**3. `TimeProvider` 優於 `DateTime.Now`／`DateTime.UtcNow`**

注入 `TimeProvider` 的程式碼可以用 `FakeTimeProvider` 精確凍結、快轉、驗證時間相依邏輯；直接呼叫 `DateTime.Now` 的程式碼無法被注入的假時鐘控制，測試只能用相對值（如「30 天前」）或容忍範圍驗證，精確度與穩定性都不如前者（壓力驗證 S2／S4 對照）。

**4. 靜態方法呼叫會讓目標被判為 `legacy`，限縮成 Characterization Test**

方法內混用靜態工具類別呼叫時，工作流程的 `targetType` 判定傾向歸類為 `legacy`，測試策略隨之改變：驗證「現有行為」而非「應有行為」，且無法攔截或替換靜態呼叫本身，只能依賴其真實回傳值（壓力驗證 S2 驗證）。這不代表靜態工具類別不能用，只是要清楚這會讓測試策略從「行為驗證」偏向「現況鎖定」。
