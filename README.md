# dotnet-testing Agent Orchestration — Lite

[dotnet-testing-agent-orchestration-claude](https://github.com/kevintsengtw/dotnet-testing-agent-orchestration-claude)（以下稱「原版」）的 **Lite 版本**：只保留單元測試工作流程，以 **1+2 循序架構**（Author → Reviewer）取代原版的 1+4（Analyzer → Writer → Executor → Reviewer），目標是在**品質與 coverage 不下降**的前提下降低 token 用量。

---

## 成果摘要

以下數字皆為實測值，來源見各連結，不做行銷式改寫：

| 面向 | 結果 | 詳細報告 |
|---|---|---|
| **Token 用量** | net10、4 個代表 target、n=3 中位數：input 降幅 **31.4%～58.5%**、output 降幅 **9.0%～82.7%**，降幅隨目標複雜度（依賴數、場景規模）擴大而擴大 | [docs/COMPARISON_REPORT.md](docs/COMPARISON_REPORT.md) §3 |
| **品質／coverage** | 4 target × 3 framework（net8/9/10）× 2 側，共 **24 格全數不低於原版**；其中 2 個 target（`OrderValidator`、`OrderProcessingService`）**coverage 明確優於原版**；規則／分支三方對帳無遺漏，testable 缺口皆為 0；45 次 headless 呼叫、42 個有效樣本格，零測試失敗、零 flaky | [docs/COMPARISON_REPORT.md](docs/COMPARISON_REPORT.md) §4 |
| **可移植性** | 4 步驟稽核（範例去名化、去名化後重驗、部署邊界宣告、隔離環境決定性測試）全數通過；在兩個 repo 之外、只含部署單位檔案的全新專案，對工作流程從未見過的類別跑完整流程，測試全綠、coverage 覆核一致，未發現任何依賴 lab 環境的行為 | [docs/COMPARISON_REPORT.md](docs/COMPARISON_REPORT.md) §6 |
| **壓力驗證** | 5 個刻意設計的高難度樣本（含 1 題兩輪對照）共 **6 輪執行，0 次需要修改 agent 定義檔的工作流程缺陷**；驗證了陷阱偵測能力不依賴原始碼註解提示、1+2 架構的獨立審查機制能正確攔截 Author 的撰寫疏漏 | 完整記錄保存於私有 lab repo |

---

## 架構

```text
主 session（dotnet-testing-lite-orchestrator-unit）
  → Agent: dotnet-testing-lite-author    分析＋場景推導＋撰寫＋建置修正至全綠
  → Agent: dotnet-testing-lite-reviewer  獨立審查＋驗證執行＋目標類別 coverage
```

**硬性約束**：嚴禁平行呼叫 agent；一次只處理一個類別（可範圍過濾到單一方法，多類別請求直接拒絕）；Reviewer 只審不改（工具無 Edit/Write，結構性保證審查獨立性，不是自律）。

架構設計決策與交接檔格式詳見 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

---

## 與原版的差異

| 面向 | 原版（1+4） | Lite（1+2） |
|---|---|---|
| 架構 | Analyzer → Writer → Executor → Reviewer | Author → Reviewer |
| 平行處理 | Writer 可依規模自動分割、支援多目標平行 | 嚴禁平行，單一類別上限 |
| 技能載入範圍 | `.agents/skills/` 全量 29 個可載 | 白名單 14 個＋`dotnet-test`，載入守則收緊 |
| Reviewer 產出 | 定性審查，無 coverage 實測 | 獨立執行測試＋目標類別 line/branch 實測 |
| 分析交接 | `analysis.json` 完整交接 | `scenarios.json` 精簡場景清單 |
| Legacy 程式碼測試 | 可用 reflection 測 private | 只經公開 API（Characterization Test） |
| 涵蓋的測試類型 | unit／integration／aspire／tunit 四種工作流程 | 僅 unit（xUnit），本次 benchmark 亦僅涵蓋此範圍 |
| 靜態契約大小 | ~143KB | ~50KB |

完整差異總表見 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#與原版的差異總表)。

### 何時該用原版

Lite 是原版在「單一 .NET 類別、xUnit 單元測試」這個範圍內的降本版本，**不是**原版的全面替代。遇到以下情境請改用原版：

- **一次要處理多個目標類別**：lite 的 orchestrator 會直接拒絕多類別請求
- **需要平行加速**（大型類別、大量方法、時間敏感）：lite 嚴禁平行呼叫 agent
- **需要 integration／aspire／tunit 測試工作流程**：lite 只實作了 unit 測試這一種

---

## 快速開始

1. 確認前置需求（.NET SDK、Claude Code、Node.js）並複製部署單位檔案，見 [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)
2. 依部署文件的驗證步驟，對一個簡單類別跑一次確認安裝成功
3. 開始使用：呼叫方式、Reviewer 報告怎麼讀、修改流程怎麼觸發、適用範圍與限制，見 [docs/USAGE.md](docs/USAGE.md)

---

## 致謝與來源

- 原版架構與工作流程設計：[dotnet-testing-agent-orchestration-claude](https://github.com/kevintsengtw/dotnet-testing-agent-orchestration-claude)
- 技能系列原樣引用、未修改，內容不可更改：
  - [dotnet-testing-agent-skills](https://github.com/kevintsengtw/dotnet-testing-agent-skills)（13 個 `dotnet-testing-*` 技能）
  - [unit-test-scenarios](https://github.com/kevintsengtw/unit-test-scenarios)（場景推導技能）

---

## License

[MIT](LICENSE) — 與 [dotnet-testing-agent-skills](https://github.com/kevintsengtw/dotnet-testing-agent-skills) 一致。

---

## 專案文件索引

| 文件 | 用途 |
|---|---|
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | 部署單位、安裝步驟、安裝驗證 |
| [docs/USAGE.md](docs/USAGE.md) | 呼叫方式、報告解讀、限制與實務建議 |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | 架構設計決策、與原版差異總表 |
| [docs/COMPARISON_REPORT.md](docs/COMPARISON_REPORT.md) | Lite vs 原版 benchmark 對照報告（[視覺化版](docs/COMPARISON_REPORT.html)） |
| [CLAUDE.md](CLAUDE.md) | Lab 開發文件（本 repo 的協作規範，非部署單位） |
