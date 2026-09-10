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

以下為工作流程**完整部署單位**：

> `.agents/skills/` 這個目錄本身就是技能的可用範圍：Author 與 Reviewer 只能從這裡載入，
> 目錄裡沒有的技能對工作流程而言不存在，不另設白名單清單。

```text
.claude/agents/                                  2 個定義檔
├── dotnet-testing-lite-author.md
└── dotnet-testing-lite-reviewer.md

.claude/skills/dotnet-testing-lite-orchestrator-unit/SKILL.md   主 session 指揮中心
.claude/skills/dotnet-test/                                     Author 建置前載入

.agents/skills/                                  18 個共用技能（內容不可更改）
├── unit-test-scenarios/
├── dotnet-testing-unit-test-fundamentals/
├── dotnet-testing-test-naming-conventions/
├── dotnet-testing-xunit-project-setup/
├── dotnet-testing-awesome-assertions-guide/
├── dotnet-testing-nsubstitute-mocking/
├── dotnet-testing-autofixture-basics/
├── dotnet-testing-autofixture-customization/
├── dotnet-testing-autofixture-nsubstitute-integration/
├── dotnet-testing-autodata-xunit-integration/
├── dotnet-testing-datetime-testing-timeprovider/
├── dotnet-testing-filesystem-testing-abstractions/
├── dotnet-testing-fluentvalidation-testing/
├── dotnet-testing-complex-object-comparison/
├── dotnet-testing-test-data-builder-pattern/
├── dotnet-testing-private-internal-testing/
├── dotnet-testing-test-output-logging/
└── dotnet-testing-code-coverage-analysis/

.agents/SKILLS-INDEX.md                          技能索引（由腳本自動生成）

.claude/scripts/dotnet-testing-claude-lite/       整個目錄複製（6 個檔案）
├── coverage-summary.mjs                         Cobertura → 目標類別摘要
├── coverage-summary.test.mjs                    回歸測試
├── generate-skills-index.mjs                    重新生成技能索引（--check 可驗一致性）
├── token_usage.js                               token 用量與各階段耗時
├── token_usage.test.js                          回歸測試
└── pricing.config.example.json                  成本估算單價範本
```

> **目錄名稱為什麼這麼長**：`.claude/scripts/` 是 Claude Code 的通用位置，會和你專案裡
> 其他工具放在一起。具辨識度的目錄名讓這套工作流程的腳本一眼可辨，也讓它與 full 版的
> `.claude/scripts/dotnet-testing-claude-full/` 能裝在同一個專案而不撞名。

**可選**（刪掉不影響核心工作流程）：

| 檔案 | 用途 | 刪掉會少什麼 |
|---|---|---|
| `token_usage.js`、`pricing.config.example.json` | token 用量與各階段耗時 | Orchestrator 呼叫 `token_usage.js` 的步驟以 `best-effort` 執行（失敗即略過），**不會中斷工作流程**；只是結尾少「📊 Token 用量」與「⏱ 各階段耗時」兩張表 |

上述降級行為已在隔離環境的可移植性決定性測試中實測驗證：工作流程完整跑完，
「缺失 token 計量時優雅降級、未中斷、未報錯」，詳見 [docs/COMPARISON_REPORT.md](COMPARISON_REPORT.md) §6.3。

> **耗時不再依賴 hook**：v1.2.0 起計時 hooks 已移除，各階段耗時改由 `token_usage.js`
> 自 subagent transcript 的時間窗計算。本工作流程**不安裝也不需要任何 hook**，
> 目標專案的 `settings.json` 不需要為它做任何設定。

> `CLAUDE.md` 本身**不是**部署單位，它是本 repo（lab 開發環境）的協作規範文件，工作流程所有執行規則的正本都在上述 SKILL 與 agent 定義檔內，不依賴 `CLAUDE.md` 存在。

---

## 自舊版升級（v1.1.x → v1.2.0）

v1.2.0 更動了腳本路徑並移除 hooks，**舊版的部署會失效**，需要手動處理：

```bash
# 1. 移除舊路徑的腳本（它們已搬進 dotnet-testing-claude-lite/）
rm -f  .claude/scripts/coverage-summary.mjs
rm -f  .claude/scripts/coverage-summary.test.mjs
rm -f  .claude/scripts/generate-skills-index.mjs
rm -rf .claude/scripts/token-usage

# 2. 移除計時 hooks（耗時改由 token_usage.js 提供）
rm -rf .claude/hooks

# 3. 若 settings.json 只有本工作流程的 hooks 註冊，整個刪除；
#    若還有你自己的其他設定，只移除 dotnet-testing-agent-timer-* 那兩段
```

接著照下方安裝步驟複製新的 `.claude/scripts/dotnet-testing-claude-lite/` 與更新後的
`.claude/agents/`、`.claude/skills/`。**agent 定義檔與 SKILL 每個 session 快取一次**，
更新後請重新啟動 Claude Code，否則跑到的仍是舊契約。

token 報表的寫入位置也改為 `token-usage-reports/lite/`，舊的 `token-usage-reports/`
留著不影響運作，新紀錄會從新位置重新累積。

---

## 安裝步驟

1. **複製部署單位**：把上一節列出的 `.claude/agents/`、`.claude/skills/dotnet-testing-lite-orchestrator-unit/`、`.claude/skills/dotnet-test/`、`.agents/skills/`、`.claude/scripts/dotnet-testing-claude-lite/` 複製到目標專案的對應相對路徑下（`.claude/`、`.agents/` 皆位於專案根目錄）。**不需要**任何 hook 或 `settings.json` 設定
2. **（可選）精簡**：不需要 token 與耗時報表的話，可刪除 `dotnet-testing-claude-lite/` 下的 `token_usage.js` 與 `pricing.config.example.json`
3. **確認測試專案存在**：目標測試專案（`.csproj`）已存在即可，內容可以是空的，工作流程第一次執行時會自行判斷並補上必要的 NuGet 套件（`Microsoft.NET.Test.Sdk`、`xunit`、`AwesomeAssertions`、`NSubstitute`、`AutoFixture` 等視需要）與 `ProjectReference`
4. **驗證檔案就位**：確認以下路徑都能在目標專案根目錄下找到：
   ```bash
   ls .claude/agents/dotnet-testing-lite-author.md
   ls .claude/agents/dotnet-testing-lite-reviewer.md
   ls .claude/skills/dotnet-testing-lite-orchestrator-unit/SKILL.md
   ls .claude/skills/dotnet-test/
   ls -d .agents/skills/*/ | wc -l   # 應為 18
   ls .claude/scripts/dotnet-testing-claude-lite/coverage-summary.mjs
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
5. 最終輸出包含：測試檔案連結、執行結果（通過／總數）、coverage 摘要、品質評分（`overallScore`）、使用的技能清單，以及（若保留 `token_usage.js`）「📊 Token 用量」與「⏱ 各階段耗時」兩張表

若上述五步都出現且沒有錯誤訊息，代表安裝正確。可移植性稽核中對一個全新、工作流程從未見過的類別（`LoyaltyPointsService`，2 個介面依賴＋`TimeProvider`、雙建構子）跑過完整流程並逐項驗收，結果見 [docs/COMPARISON_REPORT.md](COMPARISON_REPORT.md) §6.3，可作為「預期應該長怎樣」的參考基準。

---

## 常見問題

**Q：技能路徑不存在會發生什麼？**

Author 定義檔明訂「共用技能路徑不存在時回報錯誤並中止，不得略過技能直接工作」，這是刻意的 fail-closed 設計，不會靜默降級成「沒有這個技能就跳過」。如果 Author 回報找不到某個 `.agents/skills/<name>/SKILL.md`，代表安裝步驟 1 沒有把 `.agents/skills/` 完整複製過去，回頭檢查該目錄下是否確實有 18 個技能資料夾（見上方「驗證檔案就位」的 `wc -l` 指令）。

**Q：刪掉 `token_usage.js` 會怎樣？**

結尾少「📊 Token 用量」與「⏱ 各階段耗時」兩張表，**不會讓工作流程失敗或中斷**——
Orchestrator 呼叫它的步驟是 `best-effort`，失敗即略過。這是實測驗證過的降級行為，不是理論推測。

**Q：需要設定 hook 嗎？**

不需要。v1.2.0 起本工作流程不安裝也不需要任何 hook，目標專案的 `settings.json`
不必為它做任何設定。各階段耗時改由 `token_usage.js` 自 subagent transcript 的時間窗計算，
準確度經實測與 Claude Code 自身回報的執行時間逐項比對一致。

**Q：可以和 full 版（dotnet-testing-agent-orchestration-claude）裝在同一個專案嗎？**

可以，兩套已做過完整隔離：腳本目錄（`dotnet-testing-claude-lite/` vs `dotnet-testing-claude-full/`）、
agent 名稱、orchestrator skill 名稱、token 計量的 subagent 前綴與報表位置全部各自獨立。
唯二共用的是 `.claude/skills/dotnet-test` 與 `.agents/skills/` 的共通技能——這兩者兩套內容完全相同、
canonical 來源也相同，**刻意共用**以免上游同步變成兩條線而各自漂移。

此情境經實測驗證：同一專案同時裝入兩套後跑 lite，token 報表只列 lite 的 subagent、
只寫入自己的報表位置，full 側檔案 checksum 零變動。

**Q：測試專案的 `.csproj` 修改需要留著嗎？**

工作流程首次執行時可能會幫測試專案補上套件參考與 `ProjectReference`。如果這個測試專案是你長期使用的專案，這些修改應該留著（它們是正常運作所需要的）；如果只是拿來驗證安裝的臨時專案，驗證完可以直接捨棄整個專案，不影響部署單位本身。

**Q：一次可以測多個類別嗎？**

不行，Orchestrator 偵測到多個目標類別會直接拒絕並列出建議的逐次執行順序，不會啟動任何 agent。這是刻意的架構限制，不是安裝問題，見 [docs/USAGE.md](USAGE.md)「適用範圍與限制」。
