# CLAUDE.md

## 專案概述

dotnet-testing Agent Orchestration 的 **Lite 版本**。只保留單元測試工作流程，以 1+2 循序架構（Author＋Reviewer）取代原版 1+4，降低 token 用量且品質與 coverage 不下降。

原版：[dotnet-testing-agent-orchestration-claude](https://github.com/kevintsengtw/dotnet-testing-agent-orchestration-claude)。

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

硬性約束：**嚴禁平行呼叫 agent**；**一次只處理一個類別**（可範圍過濾到單一方法，多類別請求直接拒絕）；Reviewer 只審不改（工具無 Edit/Write）；遇權限、唯讀或 immutable 旗標阻擋時停止並回報，不以 `chmod`／`chflags` 繞過。詳見 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

## 部署單位

工作流程完整部署單位＝ `.claude/agents/`（2 個定義檔）＋ `.claude/skills/dotnet-testing-lite-orchestrator-unit` ＋ `.claude/skills/dotnet-test` ＋ `.agents/skills/`（18 個技能＋`SKILLS-INDEX.md`）＋ `.claude/scripts/coverage-summary.mjs` ＋ `.claude/scripts/generate-skills-index.mjs`（`token-usage` 與 hooks 為可選配件）。本 CLAUDE.md 非部署單位；工作流程所有執行規則的正本在 SKILL 與 agent 定義內，不依賴本檔。

安裝步驟與安裝驗證見 [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)。

## 關鍵目錄

- `.agents/skills/` — **技能池 bundled**（18 個：17 個 dotnet-testing 系列＋`unit-test-scenarios`）＋自動生成的 `SKILLS-INDEX.md`。**這個目錄本身就是可用範圍**，不另設白名單規則；技能內容**不可更改**（canonical 來源：[dotnet-testing-agent-skills](https://github.com/kevintsengtw/dotnet-testing-agent-skills) 與 [unit-test-scenarios](https://github.com/kevintsengtw/unit-test-scenarios)）
- `.claude/agents/` — 2 個 agent 定義檔
- `.claude/skills/` — `dotnet-testing-lite-orchestrator-unit`＋`dotnet-test`
- `.claude/scripts/coverage-summary.mjs` — Cobertura → 目標類別 compact 摘要（`node --test .claude/scripts/coverage-summary.test.mjs` 驗證）
- `.claude/scripts/generate-skills-index.mjs` — 重生技能索引（`--check` 驗證索引與目錄一致）
- `.claude/scripts/token-usage/` — token 計量（可選配件）
- `.claude/hooks/` — 計時 hooks（可選配件）
- `samples/unit/practice/` — net8/net9/net10 練習 fixture
- `samples/unit/pressure/` — 高難度樣本

## 技能池（目錄即可用範圍）

`.agents/skills/` 下的 18 個技能即全部可用範圍。**基礎三份每次必載**：`unit-test-fundamentals`、`test-naming-conventions`、`xunit-project-setup`；`awesome-assertions-guide` 因每次都要寫斷言，實務上同樣必載。其餘依 Author 定義檔的條件表選用。

排除項（不 bundle，因此結構上就載不到）：`bogus-fake-data`、`autofixture-bogus-integration`（Bogus 不在 lite 範圍）、`dotnet-testing-advanced-*` 八個（整合／E2E／TUnit，與單元測試無直接關係）。

新增或移除技能後執行 `node .claude/scripts/generate-skills-index.mjs` 重生索引。

載入守則：只能從 `.agents/skills/` 載入（唯一例外是 Author 建置前的 `.claude/skills/dotnet-test/SKILL.md`）；只讀 SKILL.md 本文（`references/`、`templates/` opt-in）；場景推導完成後才載技能；修正迴圈一輪批次修完。

## 常用指令

- `dotnet build samples/unit/practice/tests/Practice.Core.Tests/` — 建置單元測試
- `dotnet test samples/unit/practice/tests/Practice.Core.Tests/` — 執行單元測試
- `node --test .claude/scripts/coverage-summary.test.mjs` — 驗證 coverage 腳本
- `node .claude/scripts/generate-skills-index.mjs --check` — 驗證技能索引與目錄一致

## 測試技術棧

xUnit 2.9 + NSubstitute + AutoFixture + AwesomeAssertions + FakeTimeProvider + MockFileSystem（Bogus 不在 lite 範圍；Builder Pattern 與 private/internal 測試已納入技能池，依需求選用）

## 重要：不可更改與必守規則

- `.agents/skills/` 下所有技能內容**不可更改**，canonical 來源為上游 repo；發現錯誤時應向上游回報，不在本 repo 修改
- 修改 agent 定義檔時固定回報一行：`{檔案}: {前} → {後} bytes（{差額}），新增內容類別：結構改善｜通則`。三類內容：①結構改善（流程/職責/交接機制）②型別無關的通則 ③框架或領域專屬細節。**第三類不得進 agent 定義檔**，一律由條件式技能（`.agents/skills/`）承接。判別準則：**換一個 .NET 專案仍成立 → 通則（可進定義檔）；只在特定框架／函式庫／領域成立 → 屬技能領域**。例：「所有 public 建構子一律列入待測」＝通則；「FluentValidation 的 When 短路須逐運算元檢查」＝技能領域
- `samples/*/tests/` 是空白起點，工作流程產生的測試檔與 `.csproj` 修改**不得簽入**
- 本 repo 為公開發布版，由私有 lab repo 自動同步產生。benchmark 逐輪紀錄、契約路徑驗證與成本量測保存於 lab repo，不含於此
