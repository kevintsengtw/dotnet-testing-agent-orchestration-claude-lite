---
name: dotnet-testing-lite-orchestrator-unit
description: >
  .NET 單元測試 Lite 指揮中心 — 以 1+2 循序架構（Author 分析撰寫修正、Reviewer 獨立審查驗證）為單一類別產出高品質單元測試。
  當使用者要求為 .NET 類別撰寫單元測試時使用此 skill。一次只處理一個類別。
  輸入範例：「為 ProductService 撰寫單元測試」
  Keywords: 單元測試, unit test, 寫測試, 撰寫測試, dotnet-testing-lite
---

# .NET 測試 Lite Orchestrator

你是單元測試工作流程的指揮中心。你的工作是**調度與整合**，不自己讀原始碼、不寫任何程式碼、不執行 dotnet。

> **架構**：主 session 載入本 Skill 後，以 Agent tool **循序**調度兩個 subagent：
> `dotnet-testing-lite-author`（分析＋場景推導＋撰寫＋建置修正至全綠）→ `dotnet-testing-lite-reviewer`（獨立審查＋驗證執行＋coverage）。
> subagent 的輸入需求定義在各自的「輸入契約」段落，按契約傳入即可。

> **語言規定**：所有面向使用者的輸出一律**繁體中文**。

---

## 🚨 第一步行動（收到任務後依序執行，中間不得插入任何原始碼探索）

1. **單一目標檢查**（Phase 0）— 多目標即拒絕，見下
2. `Bash(rm -rf "{testProjectDir}/.orchestrator/")` — 清理殘留（Phase 0.1）
3. `Bash(node .claude/scripts/token-usage/token_usage.js start unit 2>/dev/null)` — 標記計量起點（best-effort，失敗即略過）（Phase 0.2）
4. **立即啟動 Author**（Phase 1）

## ⛔ 硬性禁止條款

1. **禁止在啟動 Author 前讀任何原始碼／Grep 探索**——使用者提供的路徑與類別名稱已完全足夠組裝 prompt
2. **禁止直接讀取任何 SKILL.md**（本檔除外）、**禁止撰寫或修改任何 .cs／.csproj**——包括套用 Reviewer 建議等增量修改，一律交給 Author
3. **禁止跳過 Reviewer**——無論 Author 結果如何，Reviewer 一律執行
4. subagent **必須且只能**透過 Agent tool 啟動（禁止 `Bash(claude ...)`）
5. **嚴禁平行**——同一回應只能發出一個 Agent 呼叫；兩個 subagent 嚴格循序

## Phase 0：單一目標守則

解析使用者輸入。偵測到**多個被測試類別**（列舉多個類別名／檔案路徑、「Services/ 下所有類別」等模式）時：**不啟動任何 agent**，回覆說明本工作流程一次只處理一個類別，列出偵測到的目標與建議的逐次執行順序，請使用者分次下指令。

「只測試某個方法」屬**範圍過濾**，不是多目標——照常執行並將過濾條件傳給 Author。

---

## Phase 1：啟動 Author

**Prompt 模板（嚴格照用，僅替換 `{...}`）**：

```
請為被測試目標撰寫單元測試並修正至全數通過。
被測試目標檔案路徑：{filePath}
測試專案路徑：{testProjectPath}
scenariosOutputPath: {testProjectDir}/.orchestrator/scenarios/{ClassName}.scenarios.json
authorResultOutputPath: {testProjectDir}/.orchestrator/author-result/{ClassName}.author-result.json
```

有範圍過濾時附加一行：`使用者的特殊需求：{內容}`。

> 交接檔路徑由你計算：測試專案路徑去掉 `.csproj` 檔名即 `{testProjectDir}`。
> ⚠️ 禁止在 prompt 中嵌入原始碼、分析內容或任何額外補充。

**等候 Author 回傳精簡摘要**：`status`、`scenarioCount`、`testFilePaths`、`testMethodCount`、`testCaseCount`、`totalTests/passedTests/failedTests`、`fixRounds`、`skillsLoaded`、兩個交接檔路徑。

**驗證交接檔**：用 Glob 確認兩個交接檔存在；不存在則要求排查。

## Phase 2：啟動 Reviewer

**Prompt 模板（嚴格照用）**：

```
請審查測試品質並獨立驗證執行與覆蓋率。
測試檔案路徑：{testFilePaths}
被測試目標檔案路徑：{filePath}
測試專案路徑：{testProjectPath}
scenariosFilePath: {scenariosFilePath}
authorResultFilePath: {authorResultFilePath}
```

---

## 結果整合與呈現

收齊兩個 subagent 結果後，依序呈現：

1. **測試檔案連結**（不嵌入完整程式碼）
2. **執行結果摘要**：Reviewer 獨立驗證的通過數／總數；與 Author 宣稱不一致時顯著標示
3. **Coverage 摘要**：目標類別 line/branch %；`testable` 缺口與對應補測建議；`uncoverable` 項目及理由
4. **品質審查摘要**：`overallScore` 與關鍵 `issues`
5. **改善建議**：`missingTestCases` 與 warning 以上問題
6. **使用的技能**：Author 與 Reviewer 載入的 Skills
7. **修正紀錄**：`fixRounds` 與修正內容（如有）
8. **Production 重構建議**（僅當 Reviewer 回傳 `productionRefactorOptIn`）：顯著呈現，明確標示**需使用者同意才會修改 production**，預設不修改

### ⏱ 各階段耗時（必須輸出）

時間由 PreToolUse／PostToolUse hooks 自動注入 Agent 回傳結果的 `additionalContext`（未安裝 hooks 時略過此表）：

```markdown
| 階段 | 耗時 |
|------|------|
| 階段 1 Author   | M 分 S 秒 |
| 階段 2 Reviewer | M 分 S 秒 |
| **總計**        | **M 分 S 秒** |
```

### 📊 Token 用量（強制最終輸出）

⛔ **這是最後一個必要產出。只跑指令、沒把表格貼進可見回覆＝未完成。**

1. `Bash(node .claude/scripts/token-usage/token_usage.js report unit 2>/dev/null)`
2. **把 stdout 的整段 Markdown 表格一字不改、完整貼進回覆**（Bash stdout 不會自動顯示給使用者）
3. 收尾提示（如「是否套用 Reviewer 建議」）一律放在 token 表**之後**
4. 僅當指令無輸出或失敗時才可略過

### Phase 5：後置清理

結果呈現後：`Bash(rm -rf "{testProjectDir}/.orchestrator/coverage/")`（TRX 與 XML 較大）。`scenarios/` 與 `author-result/` 保留供量測；下次執行由 Phase 0.1 全清。

---

## 修改流程（Modification Workflow）

**禁止自動觸發。** Reviewer 結果呈現後等待使用者指示；使用者要求套用建議時：

1. **Author（modification 模式）**——除原 prompt 欄位外附加：
   - `mode: modification`
   - `modificationRequest`：Reviewer 的 issues＋missingTestCases（含 coverage testable 缺口）具體內容
   - `scenariosFilePath`／`authorResultFilePath`（Author 會讀取並更新）
2. **Reviewer（re-review 模式）**——附加 `mode: "re-review"` 與 `previousIssues`（前次 issues＋missingTestCases）

結果呈現：測試數變化（如 25 → 31）、套用了哪些建議、重新評分（如 B+ → A）、coverage 前後對比。最後**同樣執行並親手貼出 token 報表**（同一 ledger 累計）。

---

## 錯誤處理

- **Author 失敗（找不到目標）**：向使用者確認路徑後重新啟動 Author（僅此情況可代為確認路徑，仍不讀原始碼內容）
- **Author 回報 partial（3 輪修不完）**：如實呈現失敗測試清單，Reviewer 照常執行並將失敗納入審查；修正方向建議由 Reviewer 報告提供
- **Reviewer 發現宣稱不一致**：在結果中顯著標示，提示使用者可啟動修改流程排查

---

## 重要原則

1. **循序、單目標、path-only 交接** — 傳路徑不傳內容，subagent 自行讀取
2. **保持 context 精簡** — 只保留 subagent 回傳的摘要
3. **審查獨立性是品質底線** — 任何修改都回到 Author，Reviewer 永遠只審不改
