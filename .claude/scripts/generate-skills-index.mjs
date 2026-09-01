#!/usr/bin/env node
// 由 .agents/skills/ 的實際內容生成技能索引。
// 目的：目錄即白名單——新增/移除技能只要重跑本腳本，不必修改 agent 定義檔。
// 用法: node .claude/scripts/generate-skills-index.mjs [--check]
import fs from "node:fs";
import path from "node:path";

const SKILLS_DIR = ".agents/skills";
const OUT = ".agents/SKILLS-INDEX.md";
// 觸發條件：以 Author Step 0 分析所得的「事實」表述，讓選用接近查表而非自由判斷。
// 新增技能時在此補一列；未列出者退回使用該技能 description 的「當需要…時使用」子句。
const TRIGGERS = {
  "dotnet-testing-unit-test-fundamentals": "基礎，每次必載",
  "dotnet-testing-test-naming-conventions": "基礎，每次必載",
  "dotnet-testing-xunit-project-setup": "基礎，每次必載",
  "dotnet-testing-awesome-assertions-guide": "每次必載（所有測試都要寫斷言）",
  "unit-test-scenarios": "Step 1 場景推導前必載",
  "dotnet-testing-nsubstitute-mocking": "目標有 `I*` 介面依賴需 Mock",
  "dotnet-testing-autofixture-basics": "需自動生成測試資料（預設含之）",
  "dotnet-testing-datetime-testing-timeprovider": "目標**建構子注入** `TimeProvider`，或需控制時間流逝／時區；靜態 `DateTime.Now` 不適用（無法以 `FakeTimeProvider` 控制）",
  "dotnet-testing-filesystem-testing-abstractions": "目標有 `IFileSystem` 依賴或檔案操作",
  "dotnet-testing-fluentvalidation-testing": "targetType 為 validator，或有 `IValidator<T>` 依賴",
  "dotnet-testing-autodata-xunit-integration": "測試專案既有 `AutoDataWithCustomization`／`InlineAutoDataWithCustomization` 定義",
  "dotnet-testing-autofixture-nsubstitute-integration": "測試專案既有 `AutoFixture.AutoNSubstitute`／`[Frozen]` 使用，**且**目標有 `I*` 介面依賴",
  "dotnet-testing-test-output-logging": "測試專案既有 `ITestOutputHelper`，或使用者要求診斷輸出",
  "dotnet-testing-complex-object-comparison": "回傳物件有**巢狀結構**、需排除欄位或處理循環參照；單層多屬性物件用 `awesome-assertions-guide` 的 `BeEquivalentTo` 即足夠，不需本技能",
  "dotnet-testing-test-data-builder-pattern": "同結構測試物件重複出現 3 次以上，或建構流程複雜到 `CreateValid{Type}()` helper 不敷使用",
  "dotnet-testing-autofixture-customization": "預設 AutoFixture 無法產生合用資料（特殊型別、DataAnnotations 限制、需自訂 SpecimenBuilder）",
  "dotnet-testing-private-internal-testing": "確有必要測 private／internal 成員時；本技能以「設計優先」為前提，反射是最後手段",
  "dotnet-testing-code-coverage-analysis": "需分析覆蓋率報告或設定 CI 覆蓋率檢查（本工作流程的 coverage 由 Reviewer 以腳本取得，通常不需要）",
};

const BASE = ["dotnet-testing-unit-test-fundamentals",
              "dotnet-testing-test-naming-conventions",
              "dotnet-testing-xunit-project-setup"];

function frontmatter(md) {
  const m = md.match(/^---\n([\s\S]*?)\n---/);
  return m ? m[1] : "";
}

// 取 description 的第一個句子（去掉 Make sure / Keywords 等機器導引語）
function summary(fm) {
  const m = fm.match(/^description:[ \t]*(\|[-+]?|>[-+]?)?[ \t]*\n?([\s\S]*?)(?=\n[a-zA-Z_-]+:|$)/m);
  if (!m) return "";
  const body = (m[2] || "")
    .split("\n").map((l) => l.replace(/^\s{2,}/, "").trim())
    .filter((l) => l && !/^(Make sure|Keywords)/i.test(l))
    .join(" ");
  const s = body.split(/(?<=。)/)[0] || body;
  return s.trim().replace(/\s+/g, " ").slice(0, 130);
}

function fallbackTrigger(fm) {
  const m = fm.match(/當([^。]*?)時使用/);
  return m ? m[1].trim() : "";
}

const dirs = fs.readdirSync(SKILLS_DIR, { withFileTypes: true })
  .filter((d) => d.isDirectory())
  .map((d) => d.name).sort();

const rows = [];
for (const name of dirs) {
  const p = path.join(SKILLS_DIR, name, "SKILL.md");
  if (!fs.existsSync(p)) continue;
  const fm = frontmatter(fs.readFileSync(p, "utf8"));
  rows.push({ name, summary: summary(fm), trigger: TRIGGERS[name] || fallbackTrigger(fm), base: BASE.includes(name) });
}

const lines = [
  "<!-- 本檔由 .claude/scripts/generate-skills-index.mjs 自動生成，請勿手動編輯。 -->",
  "# 可用技能索引",
  "",
  "本目錄下的技能即本工作流程的可用範圍——**目錄裡沒有的技能不存在**，不需要額外的白名單規則。",
  "路徑一律為 `.agents/skills/<名稱>/SKILL.md`。",
  "",
  "## 基礎（每次必載）",
  "",
];
for (const r of rows.filter((r) => r.base)) lines.push(`- **${r.name}** — ${r.summary}`);
lines.push("", "## 依需求選用（左欄為技能，右欄為觸發條件——條件成立才載入）", "", "| 技能 | 何時載入 |", "|------|---------|");
for (const r of rows.filter((r) => !r.base)) {
  lines.push(`| \`${r.name}\` | ${r.trigger || r.summary} |`);
}
lines.push("");

const out = lines.join("\n");
if (process.argv.includes("--check")) {
  const cur = fs.existsSync(OUT) ? fs.readFileSync(OUT, "utf8") : "";
  if (cur !== out) { console.error(`✗ ${OUT} 與 ${SKILLS_DIR} 不一致，請重跑本腳本`); process.exit(1); }
  console.log(`✓ ${OUT} 與實際目錄一致（${rows.length} 個技能）`); process.exit(0);
}
fs.writeFileSync(OUT, out);
console.log(`已生成 ${OUT}：${rows.length} 個技能（基礎 ${rows.filter((r) => r.base).length}），${Buffer.byteLength(out)} bytes`);
