# CLAUDE.md

## 專案概述

dotnet-testing Agent Orchestration 的 **Lite 版本**。只保留單元測試工作流程，以 1+2 循序架構（Author＋Reviewer）取代原版 1+4，降低 token 用量且品質與 coverage 不下降。

原版：[dotnet-testing-agent-orchestration-claude](https://github.com/kevintsengtw/dotnet-testing-agent-orchestration-claude)；baseline 為 claude-lab 分支 `feature/shared-skills-agents-path-separation`。

## 語言與風格

- 對話、文件、commit message 使用繁體中文
- commit message 格式：`動詞: 描述`（如 `更新:`, `重構:`, `修正:`）
- 測試方法命名：中文三段式 `方法名_情境描述_預期結果`
- 語氣直接，不用敬語
- Markdown fenced code block 必須加語言標記

## 架構（1+2 循序）

```text
主 session（dotnet-testing-lite-orchestrator-unit）
  → Agent: dotnet-testing-lite-author    分析＋場景推導＋撰寫＋建置修正至全綠
  → Agent: dotnet-testing-lite-reviewer  獨立審查＋驗證執行＋目標類別 coverage
```

硬性約束：**嚴禁平行呼叫 agent**；**一次只處理一個類別**（可範圍過濾到單一方法，多類別請求直接拒絕）；Reviewer 只審不改（工具無 Edit/Write）。詳見 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

## 部署單位

工作流程完整部署單位＝ `.claude/agents/`（2 個定義檔）＋ `.claude/skills/dotnet-testing-lite-orchestrator-unit` ＋ `.claude/skills/dotnet-test` ＋ `.agents/skills/`（14 個技能）＋ `.claude/scripts/coverage-summary.mjs`（`token-usage` 與 hooks 為可選配件）。本 CLAUDE.md 屬 lab 開發文件、非部署單位；工作流程所有執行規則的正本在 SKILL 與 agent 定義內，不依賴本檔。

## 關鍵目錄

- `.agents/skills/` — **技能白名單 bundled**（14 個：13 個 dotnet-testing 系列＋`unit-test-scenarios`），內容**不可更改**（canonical 來源：[dotnet-testing-agent-skills](https://github.com/kevintsengtw/dotnet-testing-agent-skills) 與 [unit-test-scenarios](https://github.com/kevintsengtw/unit-test-scenarios)）
- `.claude/agents/` — 2 個 agent 定義檔（本 repo 可改）
- `.claude/skills/` — `dotnet-testing-lite-orchestrator-unit`＋`dotnet-test`（本 repo 可改）
- `.claude/scripts/coverage-summary.mjs` — 唯一新增腳本：Cobertura → 目標類別 compact 摘要（`node --test .claude/scripts/coverage-summary.test.mjs` 驗證）
- `.claude/scripts/token-usage/` — token 計量（沿用原版，以 `dotnet-testing-` 前綴比對 subagent，零修改）
- `.claude/hooks/` — 計時 hooks（沿用原版，零修改）
- `samples/unit/practice/` — net8/net9/net10 驗證 fixture

## 技能白名單（Author/Reviewer 只能載入這些）

固定：`unit-test-scenarios`、`unit-test-fundamentals`、`test-naming-conventions`、`awesome-assertions-guide`；條件：`xunit-project-setup`、`nsubstitute-mocking`、`autofixture-basics`、`datetime-testing-timeprovider`、`filesystem-testing-abstractions`、`fluentvalidation-testing`、`code-coverage-analysis`（Reviewer）；進階（僅既有基礎設施觸發）：`autodata-xunit-integration`、`autofixture-nsubstitute-integration`、`test-output-logging`。另有 `dotnet-test`（Author 建置前載入）。

載入守則：白名單外禁載；只讀 SKILL.md 本文（`references/`、`templates/` opt-in）；場景推導完成後才載技能；修正迴圈一輪批次修完。

## 常用指令

- `dotnet build samples/unit/practice/tests/Practice.Core.Tests/` — 建置單元測試
- `dotnet test samples/unit/practice/tests/Practice.Core.Tests/` — 執行單元測試
- `node --test .claude/scripts/coverage-summary.test.mjs` — 驗證 coverage 腳本

## 測試技術棧

xUnit 2.9 + NSubstitute + AutoFixture + AwesomeAssertions + FakeTimeProvider + MockFileSystem（Bogus／Builder Pattern／private-internal reflection 不在 lite 範圍）

## 重要：不可更改與必守規則

- `.agents/skills/` 下所有技能內容**不可更改**；發現錯誤時寫修改建議文件至 `docs/skills/`（`SKILL_FIX_PROPOSAL_{skill-name}.md`）
- agent 定義檔的靜態大小**失控備援值**（非預算）：orchestrator SKILL ~12KB、Author ~45KB、Reviewer ~18KB。用途是偵測契約無聲膨脹，**不得作為否決結構性改善的理由**。接近或超過時的正解是拆分職責（如既定的 1+3 退路：把建置修正迴圈拆回獨立 Executor）或把內容移交條件式技能，**不是壓縮敘述或刪除正當規則**，敘述含糊導致的指令遵循失敗，成本遠高於契約多出的幾 KB。
- 修改 agent 定義檔時固定回報一行：`{檔案}: {前} → {後} bytes（{差額}），新增內容類別：結構改善｜通則`。三類內容：①結構改善（流程/職責/交接機制）②型別無關的通則 ③框架或領域專屬細節。**第三類不得進 agent 定義檔**，一律由條件式技能（`.agents/skills/`）承接。判別準則：**換一個 .NET 專案仍成立 → 通則（可進定義檔）；只在特定框架／函式庫／領域成立 → 屬技能領域**。例：「所有 public 建構子一律列入待測」＝通則；「FluentValidation 的 When 短路須逐運算元檢查」＝技能領域
- `samples/*/tests/` 是空白起點，工作流程產生的測試檔與 `.csproj` 修改**不得簽入**
- benchmark 規則保存於私有 lab repo（本 repo 為公開發布版，不含 benchmark 逐輪紀錄）
