# 架構說明 — Lite 1+2

## 設計目標

在「品質與 code coverage 不下降」的前提下降低 token 用量。原版 1+4（Analyzer → Writer → Executor → Reviewer）的三個結構性成本：被測原始碼被讀 3 次、analysis.json 是一次性中間產物、四個獨立 context 各付定義檔與重讀成本。Lite 以 1+2 循序架構消除前兩者、減半第三者。

## 工作流程

```text
主 session（dotnet-testing-lite-orchestrator-unit SKILL）
│  Phase 0    單一目標檢查（多類別直接拒絕）
│  Phase 0.1  清理 .orchestrator/ 殘留（主 session Bash 直清）
│  Phase 0.2  token_usage.js start unit（best-effort）
│
├─ Phase 1  Agent: dotnet-testing-lite-author（model: sonnet, maxTurns: 80）
│    Step 0    讀目標原始碼＋介面＋既有測試基礎設施掃描；targetType 判定
│    Step 1    載入 unit-test-scenarios → 場景推導 → 寫 .orchestrator/scenarios/{Class}.scenarios.json
│              ⚠ 硬性順序：先於任何測試碼
│    Step 2    依 requiredTechniques 從白名單載入技能（.agents/skills/，fail-closed）
│    Step 3    撰寫測試（骨架範本、中文三段式、AwesomeAssertions；大檔分批寫入）
│    Step 4    dotnet-test skill → build → test → 修正迴圈（≤3 輪，一輪批次修完）
│    Step 5    寫 .orchestrator/author-result/{Class}.author-result.json
│
├─ Phase 2  Agent: dotnet-testing-lite-reviewer（model: sonnet, maxTurns: 50，無 Edit/Write）
│    Step 1    載入 reviewerSkills（固定 3＋條件）
│    Step 2    獨立執行 dotnet test --collect:"XPlat Code Coverage"（防虛報）
│    Step 3    node coverage-summary.mjs → 目標類別 line/branch compact 摘要
│    Step 4    三方對帳（scenarios.json ↔ 測試檔 ↔ 原始碼）＋審查檢查表
│              coverage 缺口逐項判定 testable / uncoverable（uncoverable 必附反證）
│
│  Phase 3  整合呈現＋耗時表＋token 報表
└─ 修改流程（使用者同意後）：Author(modification) → Reviewer(re-review，重跑 coverage)
```

## 關鍵設計決策

1. **修正迴圈放 Author**：建置錯誤的修正需要測試碼、原始碼、技術決策脈絡，全在 Author context 內且已被 prompt cache 覆蓋，增量成本只有錯誤訊息＋diff。獨立 Executor 每次冷啟動要付定義檔＋重讀（~7.5k token）且缺撰寫脈絡。
2. **Reviewer 獨立且自己執行**：審查者沒寫過、沒修過這些測試（工具無 Edit/Write 是結構性保證），並以自己的一次 `dotnet test --collect` 同時取得執行事實與 coverage，coverage 蒐集零額外執行成本。
3. **場景清單先行**：合併 Analyzer 後的紀律保險，Author 必須先把場景推導結果落檔，Reviewer 三方對帳，原始碼的 throw／分支／guard 若未入清單即為推導疏漏。
4. **uncoverable 必附反證**：coverage 缺口宣告「不可覆蓋」必須說明為何無法從公開 API 觸發；`When`/`Unless` 中的短路條件預設視為可覆蓋。
5. **嚴禁平行**：原版的 Writer 分割、多目標平行、跨檔一致性審查隨之整批移除。
6. **退路**：若實測發現 Author context 過長影響品質，可將 Step 4 拆回獨立 Executor（1+3），定義檔已將該段寫為獨立章節以降低拆分成本。

## 交接檔

```text
{testProjectDir}/.orchestrator/
├── scenarios/{Class}.scenarios.json        # 場景清單（Reviewer 對帳錨點）
├── author-result/{Class}.author-result.json
└── coverage/                               # Reviewer 執行產出（結果呈現後清理）
```

## 與原版的差異總表

| 原版 | Lite |
|------|------|
| 1+4（Analyzer/Writer/Executor/Reviewer） | 1+2（Author/Reviewer） |
| Writer 可平行分割、多目標平行 | 嚴禁平行、單一類別上限 |
| 技能於 `.agents/skills/` 全量 29 個可載 | 白名單 14＋dotnet-test，載入守則收緊 |
| Reviewer 定性審查、無 coverage 實測 | Reviewer 獨立執行＋目標類別 line/branch 實測 |
| analysis.json 完整交接 | scenarios.json 精簡清單 |
| 場景推導規則寫在 Analyzer 定義檔 | 載入 unit-test-scenarios 技能＋定義檔專項補充 |
| legacy 可用 reflection 測 private | 只經公開 API（Characterization） |
| 使用者場景採用（Phase 0.6） | 移除 |
| 靜態契約 ~143KB | ~50KB |
