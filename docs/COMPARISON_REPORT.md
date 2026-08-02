# Lite 版 vs 原版 Benchmark 對照報告

> **關於本文件引用的 commit hash**：本文件引用的 commit hash 屬開發期歷史，保存於私有開發 repo（`dotnet-testing-agent-orchestration-claude-lite-lab`）；本公開 repo 為發布時的單一初始 commit，hash 僅供內部追溯，不對應本 repo 的 git 歷史。

日期：2026-07-31
比較對象：`dotnet-testing-agent-orchestration-claude-lite`（1+2 循序架構）vs `dotnet-testing-agent-orchestration-claude-lab` 分支 `feature/shared-skills-agents-path-separation` @ `08c8ea5`（原版 1+4 架構）

> 本報告為資料蒐集與稽核階段（Stage 1、Stage 2、可移植性稽核）完成後的彙整結論文件。所有數字引用自私有 lab repo「量測紀錄」章節的實測值，未經改寫。視覺化版本見 `docs/COMPARISON_REPORT.html`。逐輪原始執行紀錄與 artifacts（TRX、Cobertura、scenarios.json、ledger.jsonl）保存於私有 lab repo，需要覆核時可提供。

---

## 1. 結論摘要

Lite 版（1+2 循序：Author → Reviewer）相對原版（1+4：Analyzer → Writer → Executor → Reviewer）在四項通過標準上**全數滿足**：

| # | 標準 | 結果 |
|---|---|---|
| 1 | 目標類別 line/branch coverage 不低於 baseline | ✅ 達標：4 個 target × 3 個 framework（net8/9/10）共 24 格全數不低於，其中 **2 個 target（OrderValidator、OrderProcessingService）明確優於** baseline |
| 2 | 規則／分支三方對帳無遺漏、testable 缺口 = 0 | ✅ 達標：所有已完成樣本 Reviewer 逐條核對無遺漏，testable 缺口皆為 0 |
| 3 | 測試全綠 | ✅ 達標：45 次 `claude -p` 呼叫、42 個有效樣本格，**零測試失敗、零 flaky** |
| 4 | token 合計顯著下降 | ✅ 達標：net10 四個 target n=3 中位數：**input 降幅 31.4%～58.5%**、**output 降幅 9.0%～82.7%**，降幅隨目標複雜度上升而擴大 |

**額外發現（非通過標準要求，但屬正面外部效應）**：Stage 1／Stage 2 過程中為修正 coverage run-to-run 漂移而新增的「建構子強制列管」agent 定義規則，使 lite 在 `OrderValidator`（validator，跨 net8/9/10 三個 framework 皆穩定）與 `OrderProcessingService`（multi-dependency service，net10）兩個 target 上的 coverage **結構性優於** baseline，而非僅僅「不回退」。

**可移植性**：四步驟稽核（去除範例洩漏、去名化後重驗、部署邊界宣告、隔離環境決定性測試）全數通過。品質規則確認為通用敘述驅動、非依賴 `samples/` 具體類別名的示例記憶效應；部署單位（2 個 agent 定義檔 + 2 個 skill + 14 個共用技能 + 1 個 coverage 腳本）在完全隔離、不含 lab 專屬檔案（含可選配件皆排除）的環境下可正常運作完整工作流程。

---

## 2. 方法論

### 2.1 Baseline 定義

- **分支**：`dotnet-testing-agent-orchestration-claude-lab` 的 `feature/shared-skills-agents-path-separation`
- **Commit**：`08c8ea5`（2026-07-26，「重構: unit-test-scenarios 外部化，文件對齊 .agents/skills 新結構」）
- **架構**：原版 1+4（`dotnet-testing-analyzer` → `dotnet-testing-writer` → `dotnet-testing-executor` → `dotnet-testing-reviewer`）
- **取出方式**：detached `git worktree`（釘死於上述 commit，不建本地分支、量測期間程式碼絕不漂移），置於兩個 repo 之外的獨立目錄，避免與 lite 側的 `samples/` 還原程序互相干擾
- Baseline 側**無** `unit-test-scenarios` skill（該分支尚未納入），屬版本設計差異，**未補、未改**，刻意保留此差異以反映兩版本當時的真實狀態

### 2.2 量測機制（headless）

- 兩側皆以 `claude -p "<prompt>" --model sonnet --permission-mode bypassPermissions --output-format text` 執行，**一 run 一個全新 session**（避免主 session token 口徑被互動式對話 context 污染、避免 agent 定義 session 快取陷阱、確保主 session 與 subagent 模型口徑一致）
- Prompt 措辭兩側逐字相同（僅 skill 名稱不同，此為兩版本入口名稱的必然差異）
- baseline 照原樣執行（含其在 `methodCount>5 || scenarioCount>20` 時自動觸發的平行 Writer 分割），**不干預、不關閉**
- 詳細機制設計（worktree 佈局、token 擷取、artifacts 佈局、風險清單、輔助腳本 `bench-run.sh`）見私有 lab repo「執行機制」章節，此處不重複全文

### 2.3 n 配置與中位數口徑

| 層 | 範圍 | n | 理由 |
|---|---|---|---|
| 品質／coverage 回退檢查 | 4 targets × net8/net9/net10 | n=1 | coverage 與規則對帳確定性高，n=1 足以抓回退；任一格低於對照值即加跑 1 次確認（連兩次才算回退） |
| Token 中位數比較 | net10 × 4 targets | n=3 | n<3 的中位數無法解析 10~15% 效果量；技能載入內容對 framework 版本不敏感，只在 net10 量 |

### 2.4 同一把 coverage 尺

baseline 的 Reviewer 不量 coverage（無 `--collect`、無腳本），lite 的 Reviewer 量。為避免口徑不同，**兩側一律由驅動端於 run 結束後、還原 samples 之前，以 lite 的同一支 `coverage-summary.mjs` 再量一次，並以此數字為權威**；lite 側 Reviewer 自報的 coverage 僅作 integrity 交叉檢查，不進比較表。

### 2.5 環境重置與證據保存

- 每 run 前：`git checkout -- samples/ && git clean -fdq samples/`＋清 bin/obj/.orchestrator，並以 `git status --porcelain samples/` 驗證乾淨、結果記入該 run 的 `pre-run-check.txt`
- 每 run 後：先複製 handoff／tests／token-report／ledger 最後一筆＋跑驅動端 coverage 量測存證，**才**執行還原
- ledger 驗收條件：run 結束後 `ledger.jsonl` 必須新增一筆，沒有則該 run 作廢重跑（無法事後補算）
- 全程串行執行，任一時刻僅一個 `claude -p` 在跑

---

## 3. Token 結果

### 3.1 四個 target 中位數對比（net10，n=3，含快取 input／output）

| Target | baseline input 中位數 | lite input 中位數 | input 降幅 | baseline output 中位數 | lite output 中位數 | output 降幅 |
|---|---:|---:|---:|---:|---:|---:|
| TemperatureConverter（pure function） | 4,378,433 | 2,808,472 | **↓35.9%** | 52,904 | 27,263 | **↓48.5%** |
| OrderValidator（validator） | 5,295,504 | 3,632,329 | **↓31.4%** | 53,755 | 48,918 | ↓9.0% |
| SubscriptionService（service） | 6,999,817 | 4,356,495 | **↓37.8%** | 75,663 | 42,344 | **↓44.0%** |
| OrderProcessingService（multi-dependency service） | 9,888,447 | 4,104,345 | **↓58.5%** | 85,141 | 14,695 | **↓82.7%** |

### 3.2 降幅隨複雜度擴大的趨勢

input 降幅從 pure function／validator 的 31～36%，隨依賴數與場景規模上升到 service／multi-dependency service 的 38～59%；output 降幅除 OrderValidator（9.0%）外皆 ≥44%，`OrderProcessingService` 達 82.7%。推測與 1+4→1+2 省去獨立 Analyzer／Executor 階段的固定開銷在複雜目標上佔比更小、但省下的跨 agent 交接與重複讀檔次數隨依賴數增加而增加有關。

### 3.3 output 降幅變動的解釋：baseline 平行 Writer 重複產出

baseline 的 `writer` scope 在 4 個 target 中有 3 個（TemperatureConverter、SubscriptionService、OrderProcessingService）三輪皆觸發 `methodCount>5 || scenarioCount>20` 平行分割成 2 個 Writer subagent，各自產出獨立測試檔（duplicate 部分骨架與 using）；僅 `OrderValidator`（`targetType: validator`）因原版「validator 類別永不分割」規則未觸發分割。這解釋了：

- OrderValidator 的 output 降幅明顯低於其他三個 target（9.0% vs 44～83%），baseline 未平行分割時的單一 Writer 產出量本就較精簡，lite 相對之下的省幅較小
- 其餘三個 target 的 output 降幅顯著更大，baseline 平行 2 個 Writer 的重複產出（各自 constructor／field／using 骨架）被 lite 單一 Author 一次到位的寫法省去

**故 wallclock 僅 OrderValidator 一個 target 可兩側直接比較**（baseline 中位數 ≈10.6 分 vs lite ≈8.1 分）；其餘 3 個 target baseline 為平行執行，wallclock 不可比，但此限制**不影響 token 中位數比較的有效性**（token 計量的是實際用量，與 wallclock 平行與否無關）。

### 3.4 離散度觀察

| Target | lite input 中位數 | lite 最小偏移 | lite 最大偏移 | baseline 同指標偏移（對照） |
|---|---:|---:|---:|---|
| TemperatureConverter | 2,808,472 | −24.2% | +11.7% | −4.5%／+10.9% |
| OrderValidator | 3,632,329 | −22.7% | +11.1% | −10.9%／+10.1% |
| SubscriptionService | 4,356,495 | −2.5% | +16.5% | −3.5%／+16.5% |
| OrderProcessingService | 4,104,345 | −11.1% | +26.9% | −8.1%／+0.5% |

lite 側的 token 離散度在 2 個 target（TemperatureConverter、OrderValidator）明顯大於 baseline，另 2 個（SubscriptionService、OrderProcessingService）與 baseline 相近或略高，**未觀察到「target 越複雜、lite 越收斂」或「validator 比 pure function 更收斂」的規律**，離散度似乎是 1+2 架構的普遍特徵而非特定 target 類型的產物。經查 `OrderProcessingService` output 離散度最大的一輪（lite r2 為 34,183，比 r1/r3 高 2～3 倍），`author-result.json` 的 `fixRounds` 三輪皆為 0（非建置修正迴圈造成），真正原因是該輪 Author 場景推導規模本身較大（37 個測試方法／場景，明顯多於其餘兩輪的 30～31 個），**此為 Author 單一 agent 承擔分析＋撰寫＋除錯全流程時，場景推導顆粒度本身的 run-to-run 變異，屬 1+2 架構的已知取捨**，不影響「token 中位數顯著下降」的比較口徑（協定本就用中位數而非單輪值）。

---

## 4. 品質結果

### 4.1 Stage 1 + Stage 2 完整矩陣（4 target × 3 framework × 2 側）

| Target | Framework | baseline line/branch | lite line/branch | 判定 |
|---|---|---|---|---|
| TemperatureConverter | net8 | 100%/100% | 100%/100% | 平手，通過 |
| TemperatureConverter | net9 | 100%/100% | 100%/100% | 平手，通過 |
| TemperatureConverter | net10（n=3，3 輪皆同） | 100%/100% | 100%/100% | 平手，通過 |
| OrderValidator | net8 | 88.57%/50%（`[17,18,19,96]`） | **97.14%/50%**（`[96]`） | **lite 優，通過** |
| OrderValidator | net9 | 88.57%/50%（`[17,18,19,96]`） | **97.14%/50%**（`[96]`） | **lite 優，通過** |
| OrderValidator | net10（n=3，3 輪皆同） | 88.57%/50%（`[17,18,19,96]`） | **97.14%/50%**（`[96]`） | **lite 優，通過** |
| SubscriptionService | net8 | 100%/100% | 100%/100% | 平手，通過 |
| SubscriptionService | net9 | 100%/100% | 100%/100% | 平手，通過 |
| SubscriptionService | net10（n=3，3 輪皆同） | 100%/100% | 100%/100% | 平手，通過 |
| OrderProcessingService | net8（確認輪值） | 95.45%（`[71,72]`） | **100%**（`[]`） | **lite 優，通過** |
| OrderProcessingService | net9 | 100%/100%（優於自身 net10 對照） | 100%/100% | 平手，通過 |
| OrderProcessingService | net10（n=3，3 輪皆同） | 100%/**95.45%**（`[71,72]`） | 100%/**100%**（`[]`） | **lite 優，通過** |

**全部 12 個 framework×target 組合（net8/9/10 × 4 target）× 2 側，共 24 格，無任何一格經連續兩輪確認後仍低於對照值。零回退。**

### 4.2 兩處 lite 優的證據鏈（跨 framework 一致性）

**OrderValidator**：baseline 在 net8/net9/net10 **三個 framework、共 5 次獨立執行**（net10 三輪 + net8 一輪 + net9 一輪）的 uncovered 明細**完全一致**：`[17,18,19,96]`，即 public 無參數委派建構子 `OrderValidator() : this(TimeProvider.System)` 從未被測到，屬結構性缺口（baseline 沒有本專案為修正漂移而新增的「建構子強制列管」規則）。lite 側對應的 **5 次獨立執行**的 uncovered 明細也完全一致，僅 `[96]`（`BeAfterCreatedAt` 被 `.When()` 短路的 uncoverable 死碼），委派建構子每一輪皆受測。**line coverage 恆定 97.14% vs 88.57%，跨三個 .NET 版本皆重現同一模式**，非單一 framework 或單一 run 的偶然。

**OrderProcessingService**：net10 三輪與 net8 確認輪，baseline 皆缺 `line 71/72`（`paymentResult.ErrorMessage ?? "..."` 的 null-coalescing 分支），經讀取原始碼確認為可測缺口（Mock `IPaymentGateway.ChargeAsync` 回傳 `ErrorMessage=null` 即可覆蓋），baseline 各輪 Writer 皆未涵蓋此案例。lite 對應各輪 uncovered 明細皆為空。**branch coverage 100% vs 95.45%，net9 與 net10 兩個 framework 上 baseline 亦重現同一缺口**（net9 該輪例外地達到 100%，屬單一樣本的正向波動，見第 5 節）。

### 4.3 表述保守原則

本報告刻意區分「不回退」與「明確優於」兩種判定：4 個 target 中僅 2 個（OrderValidator、OrderProcessingService）達到「明確優於」，另 2 個（TemperatureConverter、SubscriptionService）為「平手」（兩側皆 100%/100%，無法比較優劣，因為已無缺口可比）。**未將平手情況誇大為 lite 優勢**，也未將偶發的單輪波動（見第 5 節）計入正式判定。

---

## 5. 事件與資料效力

### 5.1 三次用量上限中止的處置

| # | 發生位置 | 訊號樣態 | 處置 | 補跑結果 |
|---|---|---|---|---|
| 1 | Stage 1 批次 B，lite SubscriptionService r3 | task notification `status: killed`，`stdout.txt` 僅含「Execution error」，`ledger.jsonl` 未新增紀錄，發生極快（約 1 分鐘），未見明確用量上限文字 | 該 run 作廢標記，停止批次 B 後續（`OrderProcessingService` 當時未開跑），停下回報等待確認 | 額度重置後補跑，`claude_exit=0`、`ledger_new_entry=true`，結果與其餘兩輪一致（100%/100%） |
| 2 | Stage 2，baseline net9 TemperatureConverter | `stdout.txt` 明確文字：「You've hit your session limit · resets 6:40pm (Asia/Taipei)」，`claude_exit_code=1`、`ledger_new_entry=false` | 立即停止 Stage 2 後續所有 run（此事件促成用量上限關鍵字清單自「usage limit」擴充為同時涵蓋「session limit」） | 額度重置後補跑，結果與 net10/net8 同 target 對照值一致（100%/100%） |
| 3 | Stage 2，lite net9 OrderProcessingService（常規 16 runs 最後一輪） | `stdout.txt`：「You've hit your session limit · resets 1:50pm (Asia/Taipei)」，`claude_exit_code=1`、`ledger_new_entry=false` | 立即停止（含原排定的 2 格確認補跑），事後確認兩側 `samples/` 皆乾淨無殘留 | 額度重置後補跑，100%/100%，與對照值一致 |

三次事件的共同特徵：**依協定即時偵測、作廢標記、停止後續，事後環境檢查（`git status --porcelain samples/`、bin/obj/.orchestrator 殘留掃描）確認無 dirty 狀態，均未污染任何比較樣本**。`bench-run.sh` 的還原步驟不使用 `set -e`，在 `claude -p` 回傳非 0 時仍正常執行完整還原流程。三次事件皆未計入正式量測樣本；補跑後的結果與同格其他樣本一致，未觀察到「補跑因額度剛重置而異常」的效應。

### 5.2 line 172 單輪變異的確認輪判定

Stage 2 `OrderProcessingService` net8 首輪出現兩側同步低於 net10 對照值：baseline branch 93.18%（`[71,72,172]`）、lite 97.73%（`[172]`），多出的 `line 172` 是 `IsWithinBusinessHours` 的複合布林條件 `time.Hour >= BusinessHoursStart && time.Hour < BusinessHoursEnd`，經讀原始碼確認為可測邊界。依判定規則加跑確認輪：兩側確認輪皆恢復到與 net10 對照值一致（baseline 95.45%、lite 100%），**判定為 n=1 單輪變異，非回退**。

值得記錄的佐證：**兩側在同一格對稱漏測同一個邊界**，同一次 run 的隨機性影響了兩個獨立側（baseline 與 lite 分屬不同 agent 定義、不同 codebase），而非單側架構問題，性質與 Stage 1 已觀察到的「場景推導規模 run-to-run 變異」一致。

### 5.3 n=1 波動雙向性

同一輪次，`baseline net9 OrderProcessingService` 反而**優於**其自身的 net10 對照值（100% vs 95.45%），同一 target、同一側，不同 framework 之間也存在正向波動。此觀察與 5.2 的負向波動一併說明：**n=1 樣本的正負向偏移皆屬預期範圍，不宜僅憑單輪數字判定回退**，這正是本協定要求「連兩次才算回退」的設計理由，且此次實測確認了該設計的必要性（若採 n=1 直接判定，`OrderProcessingService` net8 兩格將被誤判為回退）。

---

## 6. 稽核與可移植性

benchmark 期間發現 agent 定義內兩處以 `samples/` 實際類別名（`OrderValidator`、`OrderProcessingService`）作為規則示例，可能造成「規則對這兩個類別特別有效」的誤解或部署依賴。依序執行四步驟稽核：

### 6.1 示例去名化與重驗

`.claude/agents/dotnet-testing-lite-author.md` 兩處示例改名（`OrderValidator`→`ReportService`、`OrderProcessingService`→`InvoiceService`），**規則語義零變更**（commit `89fb05e`）。去名化後 headless 單輪重跑 lite OrderValidator net10：`scenarios.json` 仍含 `Constructor` 條目（2 個場景）、line coverage **97.14%**、uncovered 僅 `[96]`，與去名化前所有樣本（Stage 1/2 共 8 次 lite OrderValidator 執行）完全一致。**證明第 4 節觀察到的 coverage 優勢來自「所有 public 建構子（含無參數委派建構子）一律列入待測」這句通用敘述，而非規則文字曾點名具體類別造成的示例記憶效應**，第 3、4 節的所有結論不因此次修改而需要修正。

### 6.2 部署單位定義

CLAUDE.md 新增「部署單位」小節（commit `e498843`），明列工作流程完整部署單位：

```text
.claude/agents/（2 個定義檔：dotnet-testing-lite-author、dotnet-testing-lite-reviewer）
+ .claude/skills/dotnet-testing-lite-orchestrator-unit
+ .claude/skills/dotnet-test
+ .agents/skills/（14 個共用技能）
+ .claude/scripts/coverage-summary.mjs
（token-usage 與 hooks 為可選配件）
```

CLAUDE.md 本身宣告為 lab 開發文件、非部署單位；工作流程所有執行規則的正本在 SKILL 與 agent 定義內，不依賴該檔。

### 6.3 隔離環境驗證結果

在兩個 repo 之外的全新臨時 .NET 專案（`dotnet new classlib` + `dotnet new xunit`）中，**僅複製部署單位檔案**（不含 `CLAUDE.md`、`docs/`、`samples/`，連可選配件 `hooks`、`token-usage` 都刻意排除），對自行設計、工作流程從未見過的類別 `LoyaltyPointsService`（2 個介面依賴 + `TimeProvider`、雙建構子含委派建構子、guard、業務門檻分支、日期差值分支）執行完整 headless 工作流程：

| 驗收項 | 結果 |
|---|---|
| 兩階段循序執行（非平行） | ✅ |
| scenarios 先於測試檔落檔 | ✅ |
| 僅白名單技能載入，零 `references/`／`templates/` 讀取 | ✅ |
| 測試全綠（25/25） | ✅ 驅動端獨立覆核一致 |
| Reviewer coverage 覆核成功（100%/100%） | ✅ 驅動端獨立覆核一致 |
| 建構子強制列管規則跨專案生效 | ✅ 5 個場景涵蓋雙建構子全部 3 個 null-guard |
| 缺失可選配件（`token-usage`）時的降級行為 | ✅ 優雅降級，未中斷、未報錯 |

**未發現任何依賴 lab 環境的行為**：無 `CLAUDE.md` 讀取、無 `samples/` 路徑假設、無寫死結構、無 git 指令依賴。四步驟全數通過，Artifacts 保存於私有 lab repo。

---

## 7. 限制與未來工作

1. **lite token 離散度約 ±20%，未隨目標複雜度或類型收斂**（見 3.4 節）。原因初步歸因於 Author 單一 agent 承擔分析＋撰寫＋除錯全流程時場景推導規模本身的 run-to-run 變異，尚未有根治方案；建議後續若要進一步壓低此離散度，可考慮固定場景推導的顆粒度指引，但目前不影響「中位數顯著下降」的結論。
2. **wallclock 多數 target 不可比**：4 個 target 中僅 `OrderValidator` 因未觸發 baseline 的平行 Writer 分割而可直接比較（baseline≈10.6 分 vs lite≈8.1 分）；其餘 3 個因 baseline 平行執行而不可比，此為架構差異的必然結果，非量測缺陷。
3. **僅涵蓋 xUnit 單元測試範圍**：本次 benchmark 與 lite 版本身皆僅涵蓋原版四大工作流程（unit／integration／aspire／tunit）中的 unit 測試，且僅 xUnit（非 TUnit）。品質與 token 結論不可外推至其他測試框架或整合測試工作流程。
4. **1+3 退路預留**：若未來目標類別複雜度持續上升導致 Author context 過長、品質下滑，既定退路是把 Step 4（建置修正迴圈）拆回獨立 Executor（形成 1+3：Author → Executor → Reviewer），而非發明其他架構；本次 4 個代表 target（含 4 依賴的 `OrderProcessingService`）尚未觀察到觸發此退路的訊號（`fixRounds` 全數為 0）。
5. Baseline 側缺少 `unit-test-scenarios` skill 屬版本設計差異，本次刻意不補；若未來 baseline 分支納入該 skill，需重新量測以確認結論是否維持。

---

## 附錄

### A. Artifacts 目錄索引

以下目錄結構保存於私有 lab repo（本公開 repo 不含此目錄）：

```text
docs/benchmark-artifacts/
├─ smoke-*/                                    冒煙階段（4 target 各 1～2 輪，先於正式 Stage 1/2）
├─ pilot/net10-TemperatureConverter-r1/        headless 機制 pilot 驗證（不計入樣本）
├─ bench-stage1/{baseline,lite}/net10-{Target}-r{1,2,3}/
│                                              Stage 1：4 target × 2 側 × n=3（品質+token）
├─ bench-stage2/{baseline,lite}/net{8,9}-{Target}-r{1,2}/
│                                              Stage 2：4 target × 2 framework × 2 側 × n=1（+2 確認輪）
└─ audit-portability/
   ├─ lite/net10-OrderValidator-r1/            去名化後重驗
   └─ decisive-test/                            隔離環境可移植性決定性測試
```

各 run 目錄內含：`meta.json`（side/framework/target/run/commit/時間窗/驗收旗標）、`prompt.txt`（逐字送出的 prompt）、`stdout.txt`／`stderr.txt`、`handoff/`（交接檔）、`tests/`（產出測試檔）、`token-report.md`、`token-entry.json`（ledger 最後一筆）、`coverage.json`（驅動端權威覆核）、`coverage-raw/`（cobertura XML）、`pre-run-check.txt`（run 前 git status 驗證）。

### B. 關鍵 Commits

| Commit | 說明 |
|---|---|
| `bd2beb9` | 修正：強制列管 public 建構子場景以消除 coverage run 間漂移（本次 lite 優勢的根源修正） |
| `1d939cb` | 驗證：建構子漂移收斂＋service 類擴大驗證全通過 |
| `6f320c0` | 新增：BENCHMARK 執行機制章節（Stage 0 方案） |
| `b9108ee` | 更新：驅動端模型與核可狀態、Stage 1 分批計畫 |
| `02d784b` | 驗證：headless pilot 五項全通過，R1 風險解除 |
| `9e90f48` | 新增：Stage 1 批次 A-1 完成（TemperatureConverter net10） |
| `8f99095` | 新增：Stage 1 批次 A-2 完成（OrderValidator net10） |
| `1767bfc` | 新增：批次 B 第一段（SubscriptionService，含 lite r3 中止事件） |
| `7a475c7` | 完成：Stage 1 全部結束（四個 target net10 兩側各 n=3） |
| `f0a2b95` | 新增：Stage 2 起跑，中止於 baseline net9 TemperatureConverter |
| `4fa1ab9` | 新增：Stage 2 續跑至第一個檢查點 |
| `7e73e98` | 新增：Stage 2 續跑至 15/16，2 格待確認、1 run 中止 |
| `f8a7609` | 完成：Stage 2 全部結束，Benchmark 資料蒐集完成 |
| `89fb05e` | 重構：去除範例專案識別字，確保定義檔可移植 |
| `e498843` | 新增：CLAUDE.md 部署單位宣告 ＋ 可移植性稽核完整結果 |

完整協定、執行機制設計、逐輪明細與判定規則保存於私有 lab repo。
