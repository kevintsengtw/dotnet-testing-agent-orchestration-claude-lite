#!/usr/bin/env node
// coverage-summary.mjs — 將 Cobertura XML 壓縮成「目標類別」的 line/branch compact 摘要。
// 單一用途：只解析、只輸出 JSON 到 stdout。不執行 dotnet、不做門檻判定、不寫檔。
// 找不到目標類別時 fail-closed（exit 1），不退回 assembly 平均值。
//
// 用法：
//   node .claude/scripts/coverage-summary.mjs \
//     --coverage-dir <dir>      # 遞迴尋找最新的 coverage.cobertura.xml
//     --target-class <Name>     # 目標類別名稱（不含 namespace）
//     --target-source <path>    # 目標原始碼路徑（以檔名比對 filename 屬性）

import fs from "node:fs";
import path from "node:path";
import process from "node:process";

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i += 2) {
    const key = argv[i];
    const value = argv[i + 1];
    if (key === "--coverage-dir") args.coverageDir = value;
    else if (key === "--target-class") args.targetClass = value;
    else if (key === "--target-source") args.targetSource = value;
    else if (key === "--file") args.file = value; // 直接指定 XML（測試用）
    else throw new Error(`未知參數: ${key}`);
  }
  return args;
}

function findNewestCobertura(dir) {
  if (!fs.existsSync(dir)) return null;
  const found = [];
  const walk = (d) => {
    for (const entry of fs.readdirSync(d, { withFileTypes: true })) {
      const full = path.join(d, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.name === "coverage.cobertura.xml") found.push(full);
    }
  };
  walk(dir);
  found.sort((a, b) => fs.statSync(b).mtimeMs - fs.statSync(a).mtimeMs);
  return found[0] ?? null;
}

// 目標類別比對：cobertura class name 為 "Namespace.ClassName"，
// 巢狀／compiler-generated 為 "Namespace.ClassName/Nested" 或 "…ClassName/<Method>d__3"。
function classMatches(clsName, targetClass) {
  const lastSegment = clsName.split(".").pop() ?? "";
  return lastSegment === targetClass || lastSegment.startsWith(`${targetClass}/`);
}

export function summarize(xml, { targetClass, targetSourceFile }) {
  const lineByNumber = new Map(); // number -> { hits, branch, covered, total }
  let matchedClasses = 0;

  const classRegex = /<class\b([^>]*)>([\s\S]*?)<\/class>/g;
  const attr = (attrs, name) => {
    const m = attrs.match(new RegExp(`${name}="([^"]*)"`));
    return m ? m[1] : null;
  };

  let m;
  while ((m = classRegex.exec(xml)) !== null) {
    const attrs = m[1];
    const body = m[2];
    const name = attr(attrs, "name") ?? "";
    const filename = attr(attrs, "filename") ?? "";
    if (!classMatches(name, targetClass)) continue;
    if (targetSourceFile && path.basename(filename) !== targetSourceFile) continue;
    matchedClasses += 1;

    const lineRegex = /<line\b([^>]*?)\/?>(?:<\/line>)?/g;
    let lm;
    while ((lm = lineRegex.exec(body)) !== null) {
      const lattrs = lm[1];
      const number = Number(attr(lattrs, "number"));
      const hits = Number(attr(lattrs, "hits") ?? 0);
      const isBranch = attr(lattrs, "branch") === "True" || attr(lattrs, "branch") === "true";
      const cond = attr(lattrs, "condition-coverage"); // 例 "50% (1/2)"
      let covered = 0;
      let total = 0;
      if (isBranch && cond) {
        const cm = cond.match(/\((\d+)\/(\d+)\)/);
        if (cm) {
          covered = Number(cm[1]);
          total = Number(cm[2]);
        }
      }
      const prev = lineByNumber.get(number);
      if (prev) {
        // 同一行出現在多個（巢狀）class 區塊：取聯集（hits 取最大、branch 取覆蓋較高者）
        prev.hits = Math.max(prev.hits, hits);
        if (total > 0 && covered / total > (prev.total > 0 ? prev.covered / prev.total : -1)) {
          prev.covered = covered;
          prev.total = total;
        }
      } else {
        lineByNumber.set(number, { hits, covered, total });
      }
    }
  }

  if (matchedClasses === 0) {
    return { error: `找不到目標類別 ${targetClass}（filename=${targetSourceFile ?? "任意"}）` };
  }

  const lines = [...lineByNumber.entries()].sort((a, b) => a[0] - b[0]);
  const lineTotal = lines.length;
  const lineCovered = lines.filter(([, v]) => v.hits > 0).length;
  const uncoveredLines = lines.filter(([, v]) => v.hits === 0).map(([n]) => n);

  let branchCovered = 0;
  let branchTotal = 0;
  const uncoveredBranches = [];
  for (const [number, v] of lines) {
    if (v.total > 0) {
      branchCovered += v.covered;
      branchTotal += v.total;
      if (v.covered < v.total) {
        uncoveredBranches.push({ line: number, coveredConditions: `${v.covered}/${v.total}` });
      }
    }
  }

  const pct = (covered, total) => (total === 0 ? 100 : Math.round((covered / total) * 10000) / 100);
  return {
    className: targetClass,
    line: { covered: lineCovered, total: lineTotal, percent: pct(lineCovered, lineTotal), uncoveredLines },
    branch: { covered: branchCovered, total: branchTotal, percent: pct(branchCovered, branchTotal), uncoveredBranches },
  };
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  if (!args.targetClass) throw new Error("缺少 --target-class");
  const xmlPath = args.file ?? findNewestCobertura(args.coverageDir ?? "");
  if (!xmlPath || !fs.existsSync(xmlPath)) {
    console.log(JSON.stringify({ error: "找不到 coverage.cobertura.xml" }));
    process.exitCode = 1;
    return;
  }
  const xml = fs.readFileSync(xmlPath, "utf8");
  const targetSourceFile = args.targetSource ? path.basename(args.targetSource) : null;
  const result = summarize(xml, { targetClass: args.targetClass, targetSourceFile });
  if (result.error) {
    console.log(JSON.stringify(result));
    process.exitCode = 1;
    return;
  }
  console.log(JSON.stringify({ ...result, reportPath: xmlPath }));
}

if (process.argv[1] && path.basename(process.argv[1]) === "coverage-summary.mjs") {
  try {
    main();
  } catch (error) {
    console.error(`coverage-summary error: ${error.message}`);
    process.exitCode = 2;
  }
}
