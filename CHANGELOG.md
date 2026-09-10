# Changelog

所有重要變更都記錄於此。格式參考 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.0.0/)。

## [v1.2.0] - 2026-09-10

把 `.claude/scripts/` 下散落的腳本收進具辨識度的 `dotnet-testing-claude-lite/`，並完成與 full 版
（[dotnet-testing-agent-orchestration-claude](https://github.com/kevintsengtw/dotnet-testing-agent-orchestration-claude)）
的雙套隔離。動機是**兩套裝在同一個專案時不該互相干擾**——盤點後發現共用面遠不止目錄名稱：
`.claude/hooks/` 三個檔案與 `.claude/settings.json` 和 full 版**位元組完全相同且路徑相同**，
後裝的會直接覆蓋前一個；token 計量的 subagent 前綴兩套都是 `dotnet-testing-`，
彼此把對方的 subagent 算進自己的報表，並共寫同一份 `ledger.jsonl`。

工作流程的架構、呼叫方式、交接檔格式、Author 與 Reviewer 的職責與輸出契約**皆未變更**。
本版經六輪實跑驗證，涵蓋 net8／net9／net10 三個框架、正常／壓力／乾淨部署／雙套並存／
失敗五類情境，判準取自 transcript 的客觀事實與驅動端自行執行的 `dotnet test`。

### ⚠️ 破壞性變更（既有部署需手動升級）

腳本路徑變更且 hooks 移除，**舊版部署會失效**。升級步驟見
[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) 的「自舊版升級」一節；更新定義檔後**必須重新啟動
Claude Code**，agent 定義與 SKILL 每個 session 只快取一次，不重啟會跑到舊契約。

### 變更

- **腳本目錄命名空間化**：`coverage-summary.mjs`、`generate-skills-index.mjs` 與
  `token-usage/` 三處合併為 `.claude/scripts/dotnet-testing-claude-lite/`，六個檔案平放不再分層。
  目錄深度與原本相同，`token_usage.js` 解析 repo root 的相對層數不受影響。作法與 full 版
  v1.7.2 的 `dotnet-testing-claude-full/` 一致
- **token 計量與 full 版隔離**：`SUBAGENT_PREFIX` 由 `dotnet-testing-` 收斂為
  `dotnet-testing-lite-`；報表與狀態改寫入 `token-usage-reports/lite/` 與
  `.token-usage-state/lite/`。舊的 `token-usage-reports/` 留著不影響運作，新紀錄從新位置重新累積
- **`ROLE_ORDER` 對齊 1+2 架構**：自 v1.0.0 起沿用原版的五角色
  （`analyzer`／`writer`／`executor`），沒有 `author`。原本靠後備分支兜底所以表面正常，
  但耗時功能是依 `ROLE_ORDER` 逐一取值，不修正會讓 Author 整列從耗時表消失且不報錯
- **lab 專用工具分離**：`bench-run.sh` 與 `artifact-contamination-audit.mjs` 移入
  `.claude/scripts/lab-tools/`，不再隨發布同步——它們對外部使用者無用
  （`bench-run.sh` 的路徑是本機硬編），先前卻因 `.claude/scripts/` 整包鏡射而一併發布

### 移除

- **計時 hooks 與 `.claude/settings.json`**：`dotnet-testing-agent-timer-pre/post.sh`、
  `install-hooks.js` 與只承載 hooks 註冊的 `settings.json` 全數移除。**本工作流程不再安裝
  也不需要任何 hook**，目標專案的 `settings.json` 不必為它做任何設定

  > 移除理由是雙套共用，**不是**數字錯誤——這點與 full 版不同。full v1.7.2 移除 hook 是因為
  > Agent 背景啟動使耗時全被記成 2 秒；實測顯示該現象在 lite 沒有發生，hook 給的數字一直是準的。
  > 因此本版對替代來源的要求是「不得遜於 hook」，而非「有就好」

### 新增

- **各階段耗時改由 `token_usage.js` 提供**：耗時取自各 subagent transcript 的時間窗
  （最後一筆 − 第一筆），階段耗時為同階段最長者、循序執行則相加。`report` 的 stdout
  因此輸出兩張表（📊 Token 用量、⏱ 各階段耗時），Markdown 報告的 Subagent 明細加「耗時」欄

  準確度以實跑逐項比對 Claude Code 自身回報的執行時間，六輪十二個數字全部相符
  （例：`10:53` ↔ `finished · 10m 53s`、`1:53` ↔ `· 1m 53s`）

- **回歸保護**：`selftest` 與 `token_usage.test.js` 補上耗時斷言（階段順序、取值、
  cleanup 分流、格式）與計量隔離斷言（full 版 subagent 不進 scopes／total／durations）。
  測試項目由 71 增至 82

### 文件

- `docs/DEPLOYMENT.md` 新增「自舊版升級」整節，FAQ 新增「需要設定 hook 嗎」與
  「可以和 full 版裝在同一專案嗎」；部署清單改以實測驗證過的內容為準
- `CLAUDE.md` 新增「與 full 版刻意共用的兩處」，說明 `.claude/skills/dotnet-test` 與
  `.agents/skills/` 的 17 個交集技能為何**不**拆分：canonical 來源相同，拆成兩份會讓上游
  同步變成兩條線，v1.1.0 已因此漂移過一次

### 已知缺口

- **建置修正迴圈本批次無實跑證據**：六輪實跑的 `fixRounds` 全為 0，含 13 個依賴、
  順序相依的地獄難度壓力樣本。該路徑是否觸發取決於 Author 首輪是否寫對，非驅動端所能安排。
  本版未改動該路徑的任何程式碼與契約條文，如實記載為未驗，不因六輪全綠而推論無問題

## [v1.1.0] - 2026-08-19

技能池由 14 擴為 18 個，並把「哪些技能可用」的定義從契約內的白名單清單改為 `.agents/skills/` 目錄本身。同步上游 `dotnet-testing-agent-skills` v2.4.2。補上三類先前完全沒有條文涵蓋的行為約束：環境保護、修改模式的場景先行順序、交接檔一律以 Write／Edit 寫入。1+2 架構、呼叫方式與所有既有介面皆未變更。

本版所有契約修正都經 n≥3 的 headless 驗證，判準取自 transcript 的客觀事實（工具呼叫、檔案狀態、驅動端自行執行的 `dotnet test`），不採信 agent 自述。過程中另有兩個方向經實測**否決**，記於「實測否決的方向」一節。

### 新增

- **4 個共用技能**納入技能池：`autofixture-customization`、`complex-object-comparison`、`test-data-builder-pattern`、`private-internal-testing`。Bogus 系列（`bogus-fake-data`、`autofixture-bogus-integration`）與 `dotnet-testing-advanced-*` 八個維持排除，且改為**結構性排除**——不 bundle 進目錄，因此載不到，不需要額外的禁止條文
- **`.agents/SKILLS-INDEX.md`**：技能索引，含每個技能的觸發條件。由 `.claude/scripts/generate-skills-index.mjs` 從目錄實際內容生成，`--check` 可供 CI 驗證索引與目錄一致
- **環境保護條款**（Author 絕對規則第 7 條、Reviewer「Bash 用途」段、Orchestrator 硬性禁止第 6 條）：遇 `Permission denied`／`Operation not permitted`／唯讀或 immutable 旗標阻擋時停止並回報，禁止用 `chflags`／`chmod`／`chown`／`sudo` 取得存取。Orchestrator 另補錯誤處理：收到 subagent 回報環境阻擋時不代為排除，且不得改用其他路徑規避
- **Author／Reviewer 的 Bash 用途正面界定**：以列舉可用範圍取代全面禁止，避免影響正常的搜尋與建置能力
- **交接檔與測試檔一律以 Write／Edit 寫入**：禁止經 `python3 -c`、`cat >`、`sed -i`、`tee` 等 Bash 手法改寫。理由是可稽核性——經 Bash 的寫入不會出現在工具紀錄的 Write／Edit 項目中

### 變更

- **技能可用範圍的定義**：由契約內的白名單清單改為「`.agents/skills/` 目錄本身即可用範圍」。Author 規則 6 與 Reviewer 載入守則同步改寫
- **移除技能識別碼→路徑對照表**（1,923 bytes）：路徑改為機械組合 `.agents/skills/dotnet-testing-{識別碼}/SKILL.md`，識別碼隨之改用目錄名去前綴（`awesome-assertions` → `awesome-assertions-guide`）
- **三份基礎技能改為無條件必載**：`unit-test-fundamentals`、`test-naming-conventions`、`xunit-project-setup` 不再掛在「測試專案尚無 .cs 檔」條件下
- **`datetime-testing-timeprovider` 觸發條件措辭精確化**：由「有 `TimeProvider` 依賴／日期邏輯」改為「建構子注入 `TimeProvider`，或需控制時間流逝／時區（靜態 `DateTime.Now` 不適用，無法以 `FakeTimeProvider` 控制）」。原措辭對 legacy 目標的靜態 `DateTime.Now` 可兩邊解釋
- **`private-internal-testing` 由排除改為可選用**：Author 的「不使用 reflection 測 private 方法」改為「優先只經公開 API；確有必要時載入該技能依其設計優先原則評估」
- **Reviewer 措辭去對抗化**：命令式標記密度由 1.3/KB 降至 0.7/KB。17 個檢查項目與獨立執行、coverage 量測全數保留，只改語氣
- **移除 `scenarioCount` 回傳欄位**：該欄位無下游消費者（Orchestrator 不呈現、Reviewer 自行計數 `scenarios.json`），且已實際出現宣稱與事實不符

### 修正

- **同步上游 `dotnet-testing-agent-skills` v2.4.2**：移除技能中不存在的斷言 API 引用
- **修改模式未遵守場景先行**：`mode: modification` 下 Author 先改測試碼、事後才補 `scenarios.json`。初始流程的 Step 1 有「⚠️ 硬性順序」標記故守住，修改模式那段只是平述句，強度不足。已升級為同等強度並附理由（場景清單是可追溯的錨點，事後補寫使它退化為對帳記錄）
- **「載入與採用分離」條款於技能池改動時被誤刪**：0.6 的強制條款「偵測到設施存在 → 一律載入」被改寫為「沿用某項設施時才列入」，使載入綁在裁量決定上，而 1.4 條件表仍寫客觀事實，兩處衝突。實測 3 次中有 1 次因此漏載兩個技能。已恢復強制條款並於文件加註「本條款已被誤刪一次，請勿再以冗餘為由移除」
- **多目標判定改為 fail-closed 並列舉四種形態**：列舉多個類別／集合式指涉／主目標＋附帶／依賴巢狀。判不準時一律視為多目標
- **Phase 0 拒絕路徑補收尾約束**：禁止提議代為執行
- **條款 3 與錯誤處理的條文互斥**：「只要 Author 產出測試檔就必須執行 Reviewer」與「Author 失敗時 Reviewer 不執行」的邊界釐清

### 驗證

| 項目 | 樣本 | 結果 |
|---|---|---|
| 環境保護（Author 端，macOS `uchg`） | n=3 | 提權指令 0 次，阻擋器完好 |
| 環境保護（Reviewer 端，`chmod 000`） | n=3 | 提權指令 0 次，仍完成審查並回報 coverage 受阻 |
| 修改模式場景先行 | n=5 | 5／5 遵守（修正前 1／1 違反） |
| 交接檔一律 Write／Edit | n=2 | 經 Bash 改寫 0 次 |
| `datetime` 觸發措辭 | n=3 | service 目標仍正確載入，未誤排除 |
| 多目標拒絕（四形態） | 各 n=3 | 12／12 擋下，subagent 數 0 |
| 既有基礎設施三列觸發（種子檔） | n=3 | 3／3 載入（修正前 2／3） |

### 實測否決的方向

以下兩個方向經 n=3 對照後**未採用**，記錄於此以免重複嘗試：

- **1+3 架構（拆出獨立 Executor 負責建置修正）**：成本 +17.2%（$3.68 → $4.31），兩組範圍不重疊，測試數持平、耗時增加。Executor 本身照設計運作（7 回合、峰值 context 14K），但 Author 的回合數並未因此減少，省下的是單價最低的 cache 讀取
- **開放技能池（移除條件表，由模型依索引自行選用）**：成本 +15.6%。成因不是多載技能——載入清單與基準完全相同的那一次仍 +9.5%——而是 Author 回合數與輸出上升。改為附觸發條件的索引後仍 +16.3%，故維持條件表

## [v1.0.0] - 2026-08-08

首次公開發布。dotnet-testing Agent Orchestration 的 Lite 版本，只保留單元測試工作流程，以 1+2 循序架構（Author → Reviewer）取代原版的 1+4（Analyzer → Writer → Executor → Reviewer），在品質與 coverage 不下降的前提下降低 token 用量。

- **架構**：Orchestrator Skill 以 Agent tool 循序調度 `dotnet-testing-lite-author`（分析＋場景推導＋撰寫＋建置修正至全綠）與 `dotnet-testing-lite-reviewer`（獨立審查＋驗證執行＋目標類別 coverage）
- **硬性約束**：嚴禁平行呼叫 agent；一次只處理一個類別；Reviewer 只審不改（工具無 Edit／Write，結構性保證）
- **交接機制**：`scenarios.json` 場景清單先行落檔，Reviewer 據以三方對帳（場景清單 ↔ 測試檔 ↔ 原始碼）
- **coverage 實測**：Reviewer 自行執行 `dotnet test --collect`，經 `coverage-summary.mjs` 取得目標類別 line／branch，未覆蓋處逐項判定 `testable`／`uncoverable`
- **benchmark**：net10、4 個代表 target、n=3 中位數，input 降幅 31.4%～58.5%、output 降幅 9.0%～82.7%；4 target × 3 framework × 2 側共 24 格品質／coverage 全數不低於原版

[v1.1.0]: https://github.com/kevintsengtw/dotnet-testing-agent-orchestration-claude-lite/releases/tag/v1.1.0
[v1.0.0]: https://github.com/kevintsengtw/dotnet-testing-agent-orchestration-claude-lite/releases/tag/v1.0.0
