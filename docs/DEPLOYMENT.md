# 部署指南

面向「要把這套工作流程裝進自己 .NET 專案」的讀者。本文件只講部署，使用方式（怎麼呼叫、怎麼讀報告）見 [docs/USAGE.md](USAGE.md)。

---

## 前置需求

| 項目 | 需求 | 備註 |
|---|---|---|
| .NET SDK | 能建置、執行 xUnit 測試專案的版本 | 本專案的驗證 fixture 涵蓋 net8／net9／net10，工作流程本身不限定版本，由目標專案的 `TargetFramework` 決定 |
| Claude Code | CLI，支援 Agent／Skill 機制 | 工作流程以 `Agent` tool 循序呼叫兩個 subagent，以 `Skill` 機制載入 orchestrator |
| Node.js | 執行 `coverage-summary.mjs`（Cobertura → 目標類別摘要） | 純 ES module、無外部套件依賴，任何近代 Node 版本皆可 |
| 目標測試專案 | 已建立的 xUnit 測試專案（`.csproj`），或全新空白測試專案 | 工作流程會視需要自行補上 NuGet 套件參考與 `ProjectReference`，不會建立測試專案本身 |

技能內容來自兩個外部來源，**原樣引用、不可更改**：
- [dotnet-testing-agent-skills](https://github.com/kevintsengtw/dotnet-testing-agent-skills)
- [unit-test-scenarios](https://github.com/kevintsengtw/unit-test-scenarios)

---

## 部署單位清單

以下為工作流程**完整部署單位**（照 [CLAUDE.md](../CLAUDE.md) 已定版內容）：

```text
.claude/agents/                                  2 個定義檔
├── dotnet-testing-lite-author.md
└── dotnet-testing-lite-reviewer.md

.claude/skills/dotnet-testing-lite-orchestrator-unit/SKILL.md   主 session 指揮中心
.claude/skills/dotnet-test/                                     Author 建置前載入

.agents/skills/                                  14 個共用技能（內容不可更改）
├── unit-test-scenarios/
├── dotnet-testing-unit-test-fundamentals/
├── dotnet-testing-test-naming-conventions/
├── dotnet-testing-awesome-assertions-guide/
├── dotnet-testing-xunit-project-setup/
├── dotnet-testing-nsubstitute-mocking/
├── dotnet-testing-autofixture-basics/
├── dotnet-testing-datetime-testing-timeprovider/
├── dotnet-testing-filesystem-testing-abstractions/
├── dotnet-testing-fluentvalidation-testing/
├── dotnet-testing-code-coverage-analysis/
├── dotnet-testing-autodata-xunit-integration/
├── dotnet-testing-autofixture-nsubstitute-integration/
└── dotnet-testing-test-output-logging/

.claude/scripts/coverage-summary.mjs             唯一新增腳本：Cobertura → 目標類別摘要
```

**可選配件**（裝不裝都不影響核心工作流程）：

| 配件 | 用途 | 不裝會少什麼 |
|---|---|---|
| `.claude/scripts/token-usage/` | token 用量計量與報表 | Orchestrator 呼叫 `token_usage.js` 的步驟以 `best-effort` 執行（失敗即略過），**不會中斷工作流程**；只是結尾少一份 token 用量報表 |
| `.claude/hooks/` | 各階段耗時計時（PreToolUse／PostToolUse 注入） | Orchestrator 呈現結果時的「⏱ 各階段耗時」表格會**略過不輸出**，其餘輸出不受影響 |

上述降級行為已在隔離環境的可移植性決定性測試中實測驗證（測試時刻意排除這兩個可選配件）：工作流程完整跑完，「缺失 `token-usage` 時優雅降級、未中斷、未報錯」，詳見 [docs/COMPARISON_REPORT.md](COMPARISON_REPORT.md) §6.3。

> `CLAUDE.md` 本身**不是**部署單位，它是本 repo（lab 開發環境）的協作規範文件，工作流程所有執行規則的正本都在上述 SKILL 與 agent 定義檔內，不依賴 `CLAUDE.md` 存在。

---

## 安裝步驟

1. **複製部署單位**：把上一節列出的 `.claude/agents/`、`.claude/skills/dotnet-testing-lite-orchestrator-unit/`、`.claude/skills/dotnet-test/`、`.agents/skills/`、`.claude/scripts/coverage-summary.mjs` 複製到目標專案的對應相對路徑下（`.claude/`、`.agents/` 皆位於專案根目錄）
2. **（可選）複製可選配件**：需要 token 報表或耗時表格的話，一併複製 `.claude/scripts/token-usage/` 與 `.claude/hooks/`；若複製 hooks，確認目標專案的 Claude Code 設定（`settings.json`）有註冊對應的 PreToolUse／PostToolUse hook
3. **確認測試專案存在**：目標測試專案（`.csproj`）已存在即可，內容可以是空的，工作流程第一次執行時會自行判斷並補上必要的 NuGet 套件（`Microsoft.NET.Test.Sdk`、`xunit`、`AwesomeAssertions`、`NSubstitute`、`AutoFixture` 等視需要）與 `ProjectReference`
4. **驗證檔案就位**：確認以下路徑都能在目標專案根目錄下找到：
   ```bash
   ls .claude/agents/dotnet-testing-lite-author.md
   ls .claude/agents/dotnet-testing-lite-reviewer.md
   ls .claude/skills/dotnet-testing-lite-orchestrator-unit/SKILL.md
   ls .claude/skills/dotnet-test/
   ls .agents/skills/ | wc -l   # 應為 14
   ls .claude/scripts/coverage-summary.mjs
   ```

---

## 安裝驗證

複製完成後，建議先對一個**簡單、無外部依賴的類別**（例如一個只做算術或字串處理的純函式類別）跑一次完整流程，確認環境正確：

```text
請為 <ClassName> 撰寫單元測試
被測試目標：<path/to/ClassName.cs>
測試專案：<path/to/YourTests.csproj>
```

**預期看到的行為**（依序）：

1. Orchestrator 不讀任何原始碼即直接啟動 Author（不會有 Grep／Read 目標檔案的動作先於 Author 啟動）
2. Author 先產生 `{測試專案目錄}/.orchestrator/scenarios/{ClassName}.scenarios.json`（場景清單），**確認這一步先於任何測試檔案被寫入**
3. Author 撰寫測試檔案、執行 `dotnet build`／`dotnet test`，直到全數通過（純函式類別通常一次就綠，不需要修正迴圈）
4. Reviewer 獨立執行 `dotnet test --collect:"XPlat Code Coverage"`，透過 `coverage-summary.mjs` 取得目標類別的 line／branch coverage
5. 最終輸出包含：測試檔案連結、執行結果（通過／總數）、coverage 摘要、品質評分（`overallScore`）、使用的技能清單、（若裝了 hooks）各階段耗時表、（若裝了 token-usage）token 用量報表

若上述五步都出現且沒有錯誤訊息，代表安裝正確。可移植性稽核中對一個全新、工作流程從未見過的類別（`LoyaltyPointsService`，2 個介面依賴＋`TimeProvider`、雙建構子）跑過完整流程並逐項驗收，結果見 [docs/COMPARISON_REPORT.md](COMPARISON_REPORT.md) §6.3，可作為「預期應該長怎樣」的參考基準。

---

## 常見問題

**Q：技能路徑不存在會發生什麼？**

Author 定義檔明訂「共用技能路徑不存在時回報錯誤並中止，不得略過技能直接工作」，這是刻意的 fail-closed 設計，不會靜默降級成「沒有這個技能就跳過」。如果 Author 回報找不到某個 `.agents/skills/<name>/SKILL.md`，代表安裝步驟 1 沒有把 `.agents/skills/` 完整複製過去，回頭檢查該目錄下是否確實有 14 個技能資料夾（見上方「驗證檔案就位」的 `wc -l` 指令）。

**Q：可選配件缺失會怎樣？**

見上方「可選配件」表格：`token-usage` 缺失會讓 token 報表這一段的輸出消失，`hooks` 缺失會讓耗時表格消失，**兩者都不會讓工作流程失敗或中斷**，這是實測驗證過的降級行為，不是理論推測。

**Q：測試專案的 `.csproj` 修改需要留著嗎？**

工作流程首次執行時可能會幫測試專案補上套件參考與 `ProjectReference`。如果這個測試專案是你長期使用的專案，這些修改應該留著（它們是正常運作所需要的）；如果只是拿來驗證安裝的臨時專案，驗證完可以直接捨棄整個專案，不影響部署單位本身。

**Q：一次可以測多個類別嗎？**

不行，Orchestrator 偵測到多個目標類別會直接拒絕並列出建議的逐次執行順序，不會啟動任何 agent。這是刻意的架構限制，不是安裝問題，見 [docs/USAGE.md](USAGE.md)「適用範圍與限制」。
