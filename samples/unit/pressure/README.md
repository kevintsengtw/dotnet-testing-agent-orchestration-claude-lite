# Pressure — 壓力驗證 Fixture

這是 lite 版工作流程的**壓力測試** fixture，與 `../practice/` 並存、互不影響：

- `practice/` — 給使用者練習 Skills 的循序教學環境（Phase 1~6，難度遞增，附教學導引）
- `pressure/`（本目錄）— 給工作流程本身做**能力壓測**的環境：類別刻意設計成長方法、模糊回傳語意、職責發散等單元測試中常見的難題，用來檢驗 Author／Reviewer 在真實複雜度下是否還能正確推導場景、不誤判 coverage、不漏測

## 目錄結構

```plaintext
pressure/
├── Practice.Pressure.slnx
├── src/
│   └── Practice.Pressure.Core/      # 待測試的程式碼（單一 net10 專案）
│       ├── Interfaces/
│       └── Models/
├── tests/
│   └── Practice.Pressure.Core.Tests/  # 空白起點，只有 csproj，工作流程產生的測試檔與 csproj 修改不得簽入
└── expectations/                     # 評分基準，見下方規則
```

## 題目清單

| 代號 | 類別 | 難度 | 考點 |
|------|------|------|------|
| S1 | `ShipmentCostCalculator` | 中等 | 長方法＋3 層巢狀 if/switch，20+ 條路徑，含因前置條件互斥而不可達的路徑 |
| S2 | `LegacyMemberProfileService` | 難 | 混雜依賴＋靜態呼叫：2 介面＋1 具體類別（無介面、不可 Mock）＋靜態工具＋`DateTime.Now`（無 TimeProvider） |
| S3 | `OrderReconciliationService` | 難 | 不丟例外、以回傳值表達失敗的模糊邊界，方法命名不一致，吞例外 |
| S4 | `NotificationDispatcher` | 極難 | 非同步＋時間＋副作用交織：跨午夜靜音時段、雙通道逾時與重試、部分失敗仍整體成功 |
| S5 | `CustomerOrderProcessingService` | 地獄 | 建構子注入 13 個依賴、公開方法跨 4 個職責領域、私有欄位跨方法造成呼叫順序影響結果 |

## ⛔ `expectations/` 使用規則

`expectations/` 內的檔案是**評分基準（answer key）**，記錄每個類別「應該測到什麼」「已知陷阱」「合理的不可測項」與「預期 coverage」，用來事後比對 Author／Reviewer 的產出品質。

**這個目錄不得被工作流程讀取或引用**：

- 不放進 `src/`，不會被 orchestrator／Author／Reviewer 的正常執行路徑碰到
- 任何 prompt（含 orchestrator 呼叫 Author／Reviewer 時傳遞的內容）**不得提及 `expectations/` 路徑**
- **不得**寫入 `CLAUDE.md` 的「部署單位」章節或任何技能白名單

違反上述任一條＝洩題，該次壓測結果作廢。

## ⚠️ `src/` 註解撰寫規範（防止洩題）

`expectations/` 不是唯一的洩題管道——`src/` 底下的程式碼註解一樣可能把陷阱講給測試作者聽。判別準則只有一句：

**寫 production code 的開發者會不會寫這句話？**

- 會 → 保留。業務脈絡、歷史緣由、回傳值契約說明（例如「這裡改走排程服務的 XXX API」「回傳值語意：-1＝驗證失敗、0＝部分成功」）都是真實企業程式碼會有的註解
- 不會 → 禁止，屬洩題。任何指向「測試作者該注意什麼」「驗證時會看到什麼結果」的句子（例如「寫測試時要注意…」「這代表驗證時會看到…」「沒有重複送出檢查」這種直接點名陷阱的收尾句）都不是 production 開發者會寫的話，一律刪除或弱化成只留線索、不給答案

實務上最容易犯的錯，是在解釋完一個設計決策後，多加一句總結陷阱後果的「所以」子句——這句總結往往就是洩題的部分，砍掉即可，前面的業務脈絡可以留。

新增或修改 `src/` 內容時，逐一自問這句判別準則再落筆；既有類別如果發現類似問題，修正時**只能動註解，不能動程式碼邏輯**（行為必須與修正前完全一致），並在 commit 訊息註明。

## 執行方式

```bash
dotnet build samples/unit/pressure/src/Practice.Pressure.Core/
dotnet test samples/unit/pressure/tests/Practice.Pressure.Core.Tests/
```

啟動工作流程時比照 `practice/` 的做法，只傳被測目標檔案路徑與測試專案路徑，不附加 `expectations/` 內容。
