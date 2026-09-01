#!/usr/bin/env node
// artifact-contamination-audit.mjs
//
// 偵測某個 run 的 transcript 中，agent 是否「主動讀取」了 repo 內既有的驗證 artifacts。
//
// 為什麼需要：本 repo 同時是「受測對象」與「驗證紀錄的存放處」。每跑一個 run 就把場景
// 清單、測試檔、結果簽入 docs/，下一個測相同類別的 run 只要在 repo 內搜尋（動機通常正當，
// 如確認交接檔格式）就會撞到既有解答。實測已發生 3 次，其中一次（S2-r2）導致驅動端誤判出
// 一個不存在的「技能觸發不可重現」結論。詳見 docs/CONTRACT_PATH_COVERAGE.md。
//
// 本腳本只偵測、不防止。命中即代表該 run 對「它可能看過的那些項目」不具證據力。
//
// 用法：
//   node .claude/scripts/artifact-contamination-audit.mjs <transcript 目錄>   # 人類可讀摘要
//   node .claude/scripts/artifact-contamination-audit.mjs <目錄> --json       # JSON
//   node .claude/scripts/artifact-contamination-audit.mjs --self-test         # 自我驗證
//
// exit code：0 = 乾淨；1 = 命中（可直接當關卡用）；2 = 參數或路徑錯誤

import fs from "node:fs";
import path from "node:path";

const ARTIFACT_RE = /docs\/(contract-path|v242-verification|pressure-validation|benchmark)-artifacts/;

function scan(dir) {
  const hits = [];
  const walk = (d) => {
    for (const e of fs.readdirSync(d, { withFileTypes: true })) {
      const f = path.join(d, e.name);
      if (e.isDirectory()) { walk(f); continue; }
      if (!e.name.endsWith(".jsonl")) continue;
      for (const line of fs.readFileSync(f, "utf8").split("\n")) {
        if (!line.trim()) continue;
        let o; try { o = JSON.parse(line); } catch { continue; }
        const c = o.message?.content;
        if (!Array.isArray(c)) continue;
        for (const b of c) {
          if (b.type !== "tool_use") continue;
          const input = JSON.stringify(b.input);
          if (!ARTIFACT_RE.test(input)) continue;
          hits.push({
            file: path.basename(f),
            tool: b.name,
            detail: String(b.input.command || b.input.file_path || "").replace(/\s+/g, " ").slice(0, 160),
          });
        }
      }
    }
  };
  walk(dir);
  return hits;
}

// 自我驗證：偵測器必須對已知陽性有反應、對已知陰性無反應。
// 未通過即不可採信其結果——先前曾因腳本靜默失敗而誤把受污染的 run 判為乾淨。
function selfTest() {
  const cases = [
    { dir: "docs/v242-verification-artifacts/S2-r2/transcript", expect: "positive" },
    { dir: "docs/v242-verification-artifacts/S3/transcript", expect: "negative" },
  ];
  let ok = true;
  for (const c of cases) {
    if (!fs.existsSync(c.dir)) {
      console.log(`SKIP  ${c.expect.padEnd(8)} ${c.dir}（不存在）`);
      continue;
    }
    const n = scan(c.dir).length;
    const pass = c.expect === "positive" ? n > 0 : n === 0;
    if (!pass) ok = false;
    console.log(`${pass ? "PASS" : "FAIL"}  ${c.expect.padEnd(8)} ${c.dir} → ${n} 次命中`);
  }
  console.log(ok ? "\n偵測器有效，其結果可採信。" : "\n⚠️ 偵測器失效，不得採信任何結果。");
  return ok ? 0 : 2;
}

const args = process.argv.slice(2);
if (args.includes("--self-test")) process.exit(selfTest());

const dir = args.find((a) => !a.startsWith("--"));
if (!dir) { console.error("用法：node artifact-contamination-audit.mjs <transcript 目錄> [--json]"); process.exit(2); }
if (!fs.existsSync(dir)) { console.error(`路徑不存在：${dir}`); process.exit(2); }

const hits = scan(dir);
if (args.includes("--json")) { console.log(JSON.stringify(hits, null, 2)); process.exit(hits.length ? 1 : 0); }

if (hits.length === 0) {
  console.log(`✓ 乾淨：${dir}`);
  console.log("  未偵測到對既有 artifacts 的主動讀取。");
  process.exit(0);
}

console.log(`⚠️ 命中 ${hits.length} 次：${dir}`);
console.log("  該 run 對「它可能看過的項目」不具證據力，收集時須標記。\n");
for (const h of hits) console.log(`  [${h.tool}] ${h.file}\n    ${h.detail}`);
process.exit(1);
