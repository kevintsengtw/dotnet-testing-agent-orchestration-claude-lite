<!-- 本檔由 .claude/scripts/dotnet-testing-claude-lite/generate-skills-index.mjs 自動生成，請勿手動編輯。 -->
# 可用技能索引

本目錄下的技能即本工作流程的可用範圍——**目錄裡沒有的技能不存在**，不需要額外的白名單規則。
路徑一律為 `.agents/skills/<名稱>/SKILL.md`。

## 基礎（每次必載）

- **dotnet-testing-test-naming-conventions** — 測試命名規範與最佳實踐的專門技能。
- **dotnet-testing-unit-test-fundamentals** — .NET 單元測試基礎與 FIRST 原則的專門技能。
- **dotnet-testing-xunit-project-setup** — xUnit 測試專案建立與設定的專門技能。

## 依需求選用（左欄為技能，右欄為觸發條件——條件成立才載入）

| 技能 | 何時載入 |
|------|---------|
| `dotnet-testing-autodata-xunit-integration` | 測試專案既有 `AutoDataWithCustomization`／`InlineAutoDataWithCustomization` 定義 |
| `dotnet-testing-autofixture-basics` | 需自動生成測試資料（預設含之） |
| `dotnet-testing-autofixture-customization` | 預設 AutoFixture 無法產生合用資料（特殊型別、DataAnnotations 限制、需自訂 SpecimenBuilder） |
| `dotnet-testing-autofixture-nsubstitute-integration` | 測試專案既有 `AutoFixture.AutoNSubstitute`／`[Frozen]` 使用，**且**目標有 `I*` 介面依賴 |
| `dotnet-testing-awesome-assertions-guide` | 每次必載（所有測試都要寫斷言） |
| `dotnet-testing-code-coverage-analysis` | 需分析覆蓋率報告或設定 CI 覆蓋率檢查（本工作流程的 coverage 由 Reviewer 以腳本取得，通常不需要） |
| `dotnet-testing-complex-object-comparison` | 回傳物件有**巢狀結構**、需排除欄位或處理循環參照；單層多屬性物件用 `awesome-assertions-guide` 的 `BeEquivalentTo` 即足夠，不需本技能 |
| `dotnet-testing-datetime-testing-timeprovider` | 目標**建構子注入** `TimeProvider`，或需控制時間流逝／時區；靜態 `DateTime.Now` 不適用（無法以 `FakeTimeProvider` 控制） |
| `dotnet-testing-filesystem-testing-abstractions` | 目標有 `IFileSystem` 依賴或檔案操作 |
| `dotnet-testing-fluentvalidation-testing` | targetType 為 validator，或有 `IValidator<T>` 依賴 |
| `dotnet-testing-nsubstitute-mocking` | 目標有 `I*` 介面依賴需 Mock |
| `dotnet-testing-private-internal-testing` | 確有必要測 private／internal 成員時；本技能以「設計優先」為前提，反射是最後手段 |
| `dotnet-testing-test-data-builder-pattern` | 同結構測試物件重複出現 3 次以上，或建構流程複雜到 `CreateValid{Type}()` helper 不敷使用 |
| `dotnet-testing-test-output-logging` | 測試專案既有 `ITestOutputHelper`，或使用者要求診斷輸出 |
| `unit-test-scenarios` | Step 1 場景推導前必載 |
