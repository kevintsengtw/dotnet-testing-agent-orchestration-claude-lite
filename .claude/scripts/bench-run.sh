#!/usr/bin/env bash
# bench-run.sh <side:baseline|lite> <framework:net8|net9|net10> <Target> <runNo> [stage]
#
# 純機械 benchmark 執行輔助：組 prompt、跑 headless claude -p、收集 artifacts、量 coverage、還原 samples。
# 不判定通過與否、不比較數值、不設門檻、不重跑、不聚合——一律由人事後判讀。
# 詳見 docs/BENCHMARK.md「執行機制」章節（已核可，2026-07-28）。
set -uo pipefail

SIDE="$1"; FRAMEWORK="$2"; TARGET="$3"; RUNNO="$4"; STAGE="${5:-bench-stage1}"

LITE_DIR="/Users/user/Documents/GitHub/dotnet-testing-agent-orchestration-claude-lite"
BASELINE_DIR="/Users/user/Documents/GitHub/dotnet-testing-agent-orchestration-claude-lab-baseline"

case "$SIDE" in
  baseline) REPO="$BASELINE_DIR"; SKILL="/dotnet-testing-orchestrator-unit" ;;
  lite)     REPO="$LITE_DIR";     SKILL="/dotnet-testing-lite-orchestrator-unit" ;;
  *) echo "unknown side: $SIDE" >&2; exit 1 ;;
esac

case "$FRAMEWORK" in
  net8)  SRC_PROJ="Practice.Core.Net8"; TEST_PROJ="Practice.Core.Net8.Tests" ;;
  net9)  SRC_PROJ="Practice.Core";      TEST_PROJ="Practice.Core.Tests" ;;
  net10) SRC_PROJ="Practice.Core.Net10"; TEST_PROJ="Practice.Core.Net10.Tests" ;;
  *) echo "unknown framework: $FRAMEWORK" >&2; exit 1 ;;
esac

case "$TARGET" in
  TemperatureConverter)   REL_PATH="TemperatureConverter.cs" ;;
  OrderValidator)         REL_PATH="Validators/OrderValidator.cs" ;;
  SubscriptionService)    REL_PATH="Services/SubscriptionService.cs" ;;
  OrderProcessingService) REL_PATH="Services/OrderProcessingService.cs" ;;
  *) echo "unknown target: $TARGET" >&2; exit 1 ;;
esac

SRC_FILE_REL="samples/unit/practice/src/$SRC_PROJ/$REL_PATH"
TEST_PROJ_REL="samples/unit/practice/tests/$TEST_PROJ/$TEST_PROJ.csproj"
TEST_DIR_REL="samples/unit/practice/tests/$TEST_PROJ"
ARTIFACT_DIR="$LITE_DIR/docs/benchmark-artifacts/$STAGE/$SIDE/${FRAMEWORK}-${TARGET}-r${RUNNO}"
mkdir -p "$ARTIFACT_DIR/handoff" "$ARTIFACT_DIR/tests" "$ARTIFACT_DIR/coverage-raw"

cd "$REPO"

# 1) run 前驗證＋還原＋清冷啟動殘留（驗證結果記入 artifacts，不判定，只記錄）
PRE_STATUS_BEFORE=$(git status --porcelain samples/)
git checkout -- samples/ >/dev/null 2>&1
git clean -fdq samples/
find samples -type d \( -name bin -o -name obj -o -name .orchestrator \) -prune -exec rm -rf {} +
PRE_STATUS_AFTER=$(git status --porcelain samples/)
{
  echo "before_reset_dirty=$([ -n "$PRE_STATUS_BEFORE" ] && echo true || echo false)"
  echo "--- git status --porcelain samples/ (before reset) ---"
  echo "$PRE_STATUS_BEFORE"
  echo "--- git status --porcelain samples/ (after reset, 應為空) ---"
  echo "$PRE_STATUS_AFTER"
  echo "after_reset_clean=$([ -z "$PRE_STATUS_AFTER" ] && echo true || echo false)"
} > "$ARTIFACT_DIR/pre-run-check.txt"

# 2) 組 prompt、跑 headless claude -p
PROMPT="$SKILL
被測試目標：$SRC_FILE_REL
測試專案：$TEST_PROJ_REL"
printf '%s' "$PROMPT" > "$ARTIFACT_DIR/prompt.txt"

LEDGER_BEFORE=$(wc -l < token-usage-reports/ledger.jsonl 2>/dev/null || echo 0)
START_TS=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
claude -p "$PROMPT" --model sonnet --permission-mode bypassPermissions --output-format text \
  > "$ARTIFACT_DIR/stdout.txt" 2> "$ARTIFACT_DIR/stderr.txt"
CLAUDE_EXIT=$?
END_TS=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

# 3) 收集 handoff／tests／token 報表
find "$TEST_DIR_REL/.orchestrator" -type f -name '*.json' -exec cp {} "$ARTIFACT_DIR/handoff/" \; 2>/dev/null
find "$TEST_DIR_REL" -maxdepth 3 -type f -name '*Tests.cs' -exec cp {} "$ARTIFACT_DIR/tests/" \; 2>/dev/null
cp token-usage-reports/latest.md "$ARTIFACT_DIR/token-report.md" 2>/dev/null
LEDGER_AFTER=$(wc -l < token-usage-reports/ledger.jsonl 2>/dev/null || echo 0)
LEDGER_OK=false
if [ "$LEDGER_AFTER" -gt "$LEDGER_BEFORE" ]; then
  tail -1 token-usage-reports/ledger.jsonl > "$ARTIFACT_DIR/token-entry.json"
  LEDGER_OK=true
fi

# 4) 驅動端獨立量 coverage（同一把尺：lite 的 coverage-summary.mjs）
dotnet test "$TEST_PROJ_REL" --collect:"XPlat Code Coverage" \
  --results-directory "$ARTIFACT_DIR/coverage-raw" --verbosity minimal \
  > "$ARTIFACT_DIR/dotnet-test-independent.txt" 2>&1
node "$LITE_DIR/.claude/scripts/coverage-summary.mjs" \
  --coverage-dir "$ARTIFACT_DIR/coverage-raw" \
  --target-class "$TARGET" \
  --target-source "$REPO/$SRC_FILE_REL" \
  > "$ARTIFACT_DIR/coverage.json" 2> "$ARTIFACT_DIR/coverage-stderr.txt"
COVERAGE_EXIT=$?

# 5) meta.json（純紀錄，不判定）
cat > "$ARTIFACT_DIR/meta.json" <<EOF
{
  "side": "$SIDE", "framework": "$FRAMEWORK", "target": "$TARGET", "run": $RUNNO,
  "repo": "$REPO", "commit": "$(git rev-parse HEAD)",
  "start_utc": "$START_TS", "end_utc": "$END_TS",
  "claude_exit_code": $CLAUDE_EXIT, "ledger_new_entry": $LEDGER_OK, "coverage_exit_code": $COVERAGE_EXIT,
  "cmd": "claude -p <prompt> --model sonnet --permission-mode bypassPermissions --output-format text"
}
EOF

# 6) 還原 samples
git checkout -- samples/ >/dev/null 2>&1
git clean -fdq samples/
find samples -type d \( -name bin -o -name obj -o -name .orchestrator \) -prune -exec rm -rf {} +

echo "DONE side=$SIDE framework=$FRAMEWORK target=$TARGET run=$RUNNO claude_exit=$CLAUDE_EXIT ledger_new_entry=$LEDGER_OK -> $ARTIFACT_DIR"
