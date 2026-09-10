# dotnet-testing Agent Orchestration — Lite

用 Claude Code 的 Agent 與 Skill 機制，為單一 .NET 類別自動產出 xUnit 單元測試：分析目標、推導測試場景、撰寫測試、建置修正到全綠，再由一個獨立的審查 agent 重跑測試、量測目標類別的 line／branch coverage 並逐項審查品質。

這是 [dotnet-testing-agent-orchestration-claude](https://github.com/kevintsengtw/dotnet-testing-agent-orchestration-claude)（以下稱「原版」）的 **Lite 版本**：只保留單元測試工作流程，以 **1+2 循序架構**（Author → Reviewer）取代原版的 1+4（Analyzer → Writer → Executor → Reviewer），在**品質與 coverage 不下降**的前提下降低 token 用量。

---

## 適合誰

| 你的情況 | 這套適不適合 |
|---|---|
| 想為既有 .NET 類別補單元測試，一次處理一個類別 | 適合 |
| 希望測試風格一致（中文三段式命名、AAA、AwesomeAssertions） | 適合 |
| 想要有人幫你檢查測試漏了哪些分支 | 適合，Reviewer 會做原始碼獨立掃描與 coverage 缺口判定 |
| 一次要處理整個資料夾的類別 | **不適合**，會被直接拒絕；請改用原版 |
| 需要平行加速 | **不適合**，本工作流程嚴禁平行呼叫 agent |
| 需要 integration／aspire／TUnit 測試 | **不適合**，只實作了 unit 測試；請改用原版 |

---

## 系統需求

| 項目 | 需求 | 備註 |
|---|---|---|
| .NET SDK | 能建置並執行 xUnit 測試專案的版本 | 工作流程不限定版本，由目標專案的 `TargetFramework` 決定；驗證 fixture 涵蓋 net8／net9／net10 |
| Claude Code | CLI，支援 Agent／Skill 機制 | 以 `Agent` tool 循序呼叫兩個 subagent，以 `Skill` 機制載入 orchestrator |
| Node.js | 執行 `coverage-summary.mjs` | 純 ES module、無外部套件依賴 |
| 目標測試專案 | 已建立的 xUnit 測試專案（`.csproj`），內容可以是空的 | 工作流程會視需要補上 NuGet 套件與 `ProjectReference`，但不會替你建立測試專案 |

---

## 安裝

把以下檔案複製到你的專案根目錄下的對應位置：

```text
.claude/agents/
├── dotnet-testing-lite-author.md              分析、場景推導、撰寫、建置修正
└── dotnet-testing-lite-reviewer.md            獨立審查、驗證執行、coverage

.claude/skills/
├── dotnet-testing-lite-orchestrator-unit/     主 session 指揮中心（工作流程入口）
└── dotnet-test/                               Author 建置前載入

.agents/
├── skills/                                    18 個共用技能（內容不可更改）
└── SKILLS-INDEX.md                            技能索引（自動生成）

.claude/scripts/dotnet-testing-claude-lite/     整個目錄複製
├── coverage-summary.mjs                       Cobertura → 目標類別摘要
├── coverage-summary.test.mjs                  回歸測試
├── generate-skills-index.mjs                  重新生成技能索引
├── token_usage.js                             token 用量與各階段耗時
├── token_usage.test.js                        回歸測試
└── pricing.config.example.json                成本估算單價範本
```

目錄名稱刻意具辨識度，是為了與 full 版的 `dotnet-testing-claude-full/` 放在同一個
`.claude/scripts/` 底下也不撞名。兩套可以裝在同一個專案裡，互不干擾。

**可選**，刪掉不影響核心流程：

| 檔案 | 用途 | 刪掉會少什麼 |
|---|---|---|
| `token_usage.js`、`pricing.config.example.json` | token 用量與各階段耗時報表 | 結尾少兩張表格。Orchestrator 呼叫它的步驟以 `best-effort` 執行，失敗即略過，流程照常完成 |

這項降級行為經隔離環境實測驗證，不是理論推測。

### 確認安裝完成

```bash
ls .claude/agents/dotnet-testing-lite-author.md
ls .claude/agents/dotnet-testing-lite-reviewer.md
ls .claude/skills/dotnet-testing-lite-orchestrator-unit/SKILL.md
ls .claude/skills/dotnet-test/
ls -d .agents/skills/*/ | wc -l    # 應為 18
ls .claude/scripts/dotnet-testing-claude-lite/coverage-summary.mjs
```

技能路徑不存在時，Author 會**回報錯誤並中止**，不會靜默跳過，這是刻意的 fail-closed 設計。所以上面 `wc -l` 的結果如果不是 18，先把 `.agents/skills/` 補齊再開始用。

詳細部署說明與常見問題見 [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)。

---

## 快速開始

在 Claude Code 裡輸入：

```text
/dotnet-testing-lite-orchestrator-unit
被測試目標：src/YourProject/Services/OrderService.cs
測試專案：tests/YourProject.Tests/YourProject.Tests.csproj
```

只需要**兩個路徑**。不要貼程式碼內容。Orchestrator 不會先讀原始碼，它把路徑交給 Author，由 Author 自行讀取。

只想測某個方法時，加一行：

```text
使用者的特殊需求：只測 ProcessOrder 這個方法
```

### 會發生什麼

```text
Phase 0    單一目標檢查（多類別直接拒絕，不啟動任何 agent）
Phase 1    Author    讀目標與依賴 → 載入 unit-test-scenarios → 推導場景並落檔
                     → 依需求載入技能 → 撰寫測試 → build／test 修正至全綠
Phase 2    Reviewer  載入審查技能 → 自己重跑一次 dotnet test --collect coverage
                     → 三方對帳（場景清單 ↔ 測試檔 ↔ 原始碼）→ 產出審查報告
Phase 3    整合呈現：測試檔連結、執行結果、coverage、品質評分、改善建議
```

一個中等複雜度的類別（3 個介面依賴、8 個公開方法左右）大約 8 到 10 分鐘。

### 你會拿到什麼

- **測試檔**：一個 `{ClassName}Tests.cs`，中文三段式命名、AAA 結構、AwesomeAssertions 斷言
- **場景清單**：`.orchestrator/scenarios/{ClassName}.scenarios.json`，可追溯每個測試對應哪個場景
- **coverage 實測**：目標類別的 line／branch 百分比，未覆蓋處逐項判定為「可補測」或「無法從公開 API 觸發」（後者必須附理由）
- **品質評分**：A+ 到 D，附具體 issue 與可執行的修改建議

Reviewer 的報告怎麼讀、修改流程怎麼觸發，見 [docs/USAGE.md](docs/USAGE.md)。

---

## 練習專案

repo 內含 `samples/unit/practice`，可以不用自己找目標就直接試：

```text
src/Practice.Core/                  net9（另有 .Net8／.Net10 兩份平行版本）
├── Services/                       6 個服務類別，涵蓋介面依賴、TimeProvider、IFileSystem
├── Validators/                     FluentValidation 驗證器，含巢狀 Validator
├── Legacy/                         靜態依賴、直接 File I/O 的難測程式碼
└── Interfaces/ Models/

tests/Practice.Core.Tests/          空白起點，只有 .csproj
```

例如：

```text
/dotnet-testing-lite-orchestrator-unit
被測試目標：samples/unit/practice/src/Practice.Core/Services/SubscriptionService.cs
測試專案：samples/unit/practice/tests/Practice.Core.Tests/Practice.Core.Tests.csproj
```

四種目標類型各有不同的處理路徑，值得分別試試：`SubscriptionService`（只依賴 `TimeProvider`）、`OrderProcessingService`（3 個介面 ＋ `TimeProvider` ＋ async）、`OrderValidator`（FluentValidation ＋ 巢狀）、`LegacyReportGenerator`（靜態依賴，走 Characterization Test）。

---

## 架構

```text
主 session（dotnet-testing-lite-orchestrator-unit）
  → Agent: dotnet-testing-lite-author    分析＋場景推導＋撰寫＋建置修正至全綠
  → Agent: dotnet-testing-lite-reviewer  獨立審查＋驗證執行＋目標類別 coverage
```

**硬性約束**：

- 嚴禁平行呼叫 agent，兩個 subagent 嚴格循序
- 一次只處理一個類別；可範圍過濾到單一方法，多類別請求直接拒絕
- Reviewer **只審不改**：它的工具清單裡沒有 Edit／Write，這是結構性保證，不是自律
- Author 遇到權限、唯讀或 immutable 旗標阻擋時停止並回報，不會用 `chmod`／`chflags` 繞過

架構設計決策與交接檔格式見 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

---

## 成果摘要

以下皆為實測值，來源見各連結，不做行銷式改寫：

| 面向 | 結果 | 詳細報告 |
|---|---|---|
| **Token 用量** | net10、4 個代表 target、n=3 中位數：input 降幅 **31.4%～58.5%**、output 降幅 **9.0%～82.7%**，降幅隨目標複雜度擴大而擴大 | [docs/COMPARISON_REPORT.md](docs/COMPARISON_REPORT.md) §3 |
| **品質／coverage** | 4 target × 3 framework × 2 側共 **24 格全數不低於原版**；其中 2 個 target 的 coverage 明確優於原版；45 次 headless 呼叫、42 個有效樣本格，零測試失敗、零 flaky | [docs/COMPARISON_REPORT.md](docs/COMPARISON_REPORT.md) §4 |
| **可移植性** | 4 步驟稽核全數通過；在只含部署單位檔案的全新專案、對工作流程從未見過的類別跑完整流程，測試全綠、coverage 覆核一致 | [docs/COMPARISON_REPORT.md](docs/COMPARISON_REPORT.md) §6 |
| **壓力驗證** | 5 個刻意設計的高難度樣本共 6 輪執行，**0 次需要修改 agent 定義檔的工作流程缺陷**；驗證了陷阱偵測不依賴原始碼註解提示，獨立審查機制能攔截 Author 的撰寫疏漏 | 完整記錄保存於私有 lab repo |

---

## 與原版的差異

| 面向 | 原版（1+4） | Lite（1+2） |
|---|---|---|
| 架構 | Analyzer → Writer → Executor → Reviewer | Author → Reviewer |
| 平行處理 | Writer 可依規模自動分割、支援多目標平行 | 嚴禁平行，單一類別上限 |
| 技能載入範圍 | `.agents/skills/` 全量 29 個可載 | bundled 18 個＋`dotnet-test`，目錄即可用範圍 |
| Reviewer 產出 | 定性審查，無 coverage 實測 | 獨立執行測試＋目標類別 line／branch 實測 |
| 分析交接 | `analysis.json` 完整交接 | `scenarios.json` 精簡場景清單 |
| Legacy 程式碼測試 | 可用 reflection 測 private | 優先只經公開 API（Characterization Test） |
| 涵蓋的測試類型 | unit／integration／aspire／TUnit 四種 | 僅 unit（xUnit） |

完整差異總表見 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#與原版的差異總表)。

**Lite 不是原版的全面替代**，它是原版在「單一 .NET 類別、xUnit 單元測試」這個範圍內的降本版本。超出這個範圍請用原版。

---

## 致謝與來源

- 原版架構與工作流程設計：[dotnet-testing-agent-orchestration-claude](https://github.com/kevintsengtw/dotnet-testing-agent-orchestration-claude)
- 技能系列原樣引用、未修改，內容不可更改：
  - [dotnet-testing-agent-skills](https://github.com/kevintsengtw/dotnet-testing-agent-skills)（17 個 `dotnet-testing-*` 技能）
  - [unit-test-scenarios](https://github.com/kevintsengtw/unit-test-scenarios)（場景推導技能）

---

## License

[MIT](LICENSE) — 與 [dotnet-testing-agent-skills](https://github.com/kevintsengtw/dotnet-testing-agent-skills) 一致。

---

## 文件索引

| 文件 | 用途 |
|---|---|
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | 部署單位、安裝步驟、安裝驗證、常見問題 |
| [docs/USAGE.md](docs/USAGE.md) | 呼叫方式、報告解讀、修改流程、限制與實務建議 |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | 架構設計決策、與原版差異總表 |
| [docs/COMPARISON_REPORT.md](docs/COMPARISON_REPORT.md) | Lite vs 原版 benchmark 對照報告（[視覺化版](docs/COMPARISON_REPORT.html)） |
| [CHANGELOG.md](CHANGELOG.md) | 版本變更紀錄 |
