# S1 — ShipmentCostCalculator

`CalculateCost(ShipmentRequest request)`，長方法＋3 層巢狀 if/switch。

## 1. 必測行為清單

**Null guard**
- `request == null` → 拋 `ArgumentNullException`

**Zone base rate（4 條）**
- Local → 基礎費率 50
- Regional → 90
- National → 150
- International → 380

**WeightBand multiplier（4 條）**
- Envelope ×1.0／Light ×1.25／Medium ×1.75／Heavy ×2.4

**假日加成（domesticHolidaySurcharge 區塊，3 條）**
- 非假日，或假日但 Zone=International → 不加成、不進 `AppliedSurcharges`
- 假日＋Zone=Local → +30，`"LocalHoliday"`
- 假日＋Zone=Regional／National → +60，`"DomesticHoliday"`

**易碎加成（3 層巢狀最深處，4 條）**
- 非易碎 → 跳過
- 易碎＋(Local 或 Regional)＋Heavy → +45，`"FragileHandling"`
- 易碎＋(Local 或 Regional)＋非 Heavy → +20，`"FragileHandling"`
- 易碎＋(National 或 International)＋Gold → +35，`"FragileHandlingDiscounted"`
- 易碎＋(National 或 International)＋非 Gold → +70，`"FragileHandlingFull"`

**會員折扣（3 條）**
- Gold → ×0.85／Silver → ×0.95／Standard → 不變

**回傳值**
- `TotalCost` 為 `Math.Round(cost, 2)`；`AppliedSurcharges` 內容與順序需與觸發的加成一致

## 2. 已知陷阱

- **`domesticHolidaySurcharge` 旗標的作用域**：這個 `bool` 只在「假日＋非 International」時被設為 `true`；第二個 `if (Zone == International && domesticHolidaySurcharge)` 因此恆為 `false`——測 uncovered branch 時容易誤判成「testable，只是還沒補」，實際上這是設計上互斥（見第 3 節）
- **`LegacyPromoAdjustment` 同時依賴 `Standard`＋`International`＋`IsFragile`＋`domesticHolidaySurcharge`**：由於 `domesticHolidaySurcharge` 在 `Zone == International` 時必為 `false`，這條加成**永遠不會觸發**——跟上一條是同一個根因（`domesticHolidaySurcharge` 的作用域設計），評分時應視為同一類缺陷，不是兩個獨立的巧合
- **`IsFragile` 判斷用的是「以外」而非「以上」的區域分組**：`Local`／`Regional` 一組，`National`／`International` 另一組，跟假日加成的分組（`International` 單獨一組）不是同一套邏輯，容易在推導場景時誤用同一組合

## 3. 合理的不可測項

- **`InternationalHoliday` 加成分支**（`if (Zone == International && domesticHolidaySurcharge)` 為真的那一支）：`domesticHolidaySurcharge` 只在前一個 `if` 判斷 `Zone != International` 為真時才會被設為 `true`；因此進入這個分支時 `Zone == International` 與 `domesticHolidaySurcharge == true` 無法同時成立——**可由程式碼靜態證明不可達**，不是隨機難測
- **`LegacyPromoAdjustment` 加成分支**（four 條件全真的那一支）：同上，其中的 `domesticHolidaySurcharge == true` 與同一條件內的 `Zone == International` 同樣互斥，一併不可達

以上 2 條路徑**不列入 `missingTestCases`**；判定為 `uncoverable` 時必須引用「`domesticHolidaySurcharge` 只在 `Zone != International` 時為 true」這個具體理由，籠統寫「不會發生」不合格。

## 4. 預期 coverage

- Line coverage：因兩段死碼各佔 2~3 行，實測值應落在 **90~95%** 之間，不會是 100%
- Branch coverage：22 條路徑中 2 條不可達，理論上限約 **(20/22) ≈ 90.9%**；因為兩個死碼分支共用同一個判斷式的部分節點，實際數字可能略高於此
- 若 Reviewer 回報 100% line 或 100% branch，代表死碼判定有誤（可能誤把 `domesticHolidaySurcharge` 當成隨機變數而非確定性旗標），需回頭檢查
