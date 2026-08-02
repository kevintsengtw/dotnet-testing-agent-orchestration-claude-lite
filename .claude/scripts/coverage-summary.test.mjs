import { test } from "node:test";
import assert from "node:assert/strict";
import { summarize } from "./coverage-summary.mjs";

const XML = `<?xml version="1.0" encoding="utf-8"?>
<coverage>
  <packages>
    <package name="Practice.Core">
      <classes>
        <class name="Practice.Core.TemperatureConverter" filename="src/Practice.Core/TemperatureConverter.cs">
          <lines>
            <line number="10" hits="3" branch="False" />
            <line number="12" hits="3" branch="True" condition-coverage="50% (1/2)" />
            <line number="14" hits="0" branch="False" />
            <line number="16" hits="2" branch="True" condition-coverage="100% (2/2)" />
          </lines>
        </class>
        <class name="Practice.Core.TemperatureConverter/&lt;Helper&gt;d__3" filename="src/Practice.Core/TemperatureConverter.cs">
          <lines>
            <line number="20" hits="1" branch="False" />
          </lines>
        </class>
        <class name="Practice.Core.OtherService" filename="src/Practice.Core/OtherService.cs">
          <lines>
            <line number="5" hits="0" branch="False" />
          </lines>
        </class>
      </classes>
    </package>
  </packages>
</coverage>`;

test("正常解析：只計目標類別（含巢狀 compiler-generated），排除其他類別", () => {
  const r = summarize(XML, { targetClass: "TemperatureConverter", targetSourceFile: "TemperatureConverter.cs" });
  assert.equal(r.error, undefined);
  assert.equal(r.line.total, 5); // 10,12,14,16,20（OtherService 的 line 5 不計）
  assert.equal(r.line.covered, 4);
  assert.deepEqual(r.line.uncoveredLines, [14]);
  assert.equal(r.branch.covered, 3);
  assert.equal(r.branch.total, 4);
  assert.deepEqual(r.branch.uncoveredBranches, [{ line: 12, coveredConditions: "1/2" }]);
});

test("找不到目標類別時 fail-closed 回傳 error，不退回 assembly 平均", () => {
  const r = summarize(XML, { targetClass: "NoSuchClass", targetSourceFile: null });
  assert.ok(r.error);
  assert.equal(r.line, undefined);
});

test("filename 不符時不採計（同名類別在別的檔案）", () => {
  const r = summarize(XML, { targetClass: "TemperatureConverter", targetSourceFile: "Wrong.cs" });
  assert.ok(r.error);
});

test("無 branch 資料時 branch percent 為 100（total 0 不除以零）", () => {
  const xml = `<coverage><packages><package><classes>
    <class name="A.Pure" filename="Pure.cs"><lines><line number="1" hits="1" branch="False" /></lines></class>
  </classes></package></packages></coverage>`;
  const r = summarize(xml, { targetClass: "Pure", targetSourceFile: "Pure.cs" });
  assert.equal(r.branch.total, 0);
  assert.equal(r.branch.percent, 100);
  assert.equal(r.line.percent, 100);
});
