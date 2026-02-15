---
name: testing-ui
description: Semi-autonomous UI testing with Playwright. Scans localhost UIs, generates test specs for Q approval, executes approved tests, and reports findings. Manual trigger only, not part of auto workflow.
---

# /test-ui [target]

> Scan a localhost UI, generate test-spec, get Q approval, execute tests, report findings.
> **Manual trigger only** - NOT part of auto workflow. Does not require /rev or Codex review.

## Usage Examples

```
/test-ui flow-builder
/test-ui dashboard
/test-ui http://localhost:3002/flow-builder/
/test-ui flow-builder --auth 1:my-api-key
```

## Known Targets

| Shorthand | URL |
|-----------|-----|
| `flow-builder` | `http://localhost:3002/flow-builder/` |
| `flow-builder-prod` | `http://localhost:5000/flow-builder/` |
| `dashboard` | `http://localhost:5000/ops` |
| `backend` | `http://localhost:5000/` |

---

## Devil's Advocate (PP-006)

Challenge the test scope:
- "Are we testing the right things, or just the easy things?"
- "What user flows are NOT covered by these tests?"
- "Could this pass all tests and still be broken for real users?"
- Don't report "all green" without questioning coverage gaps

---

## Execution Protocol

When Q runs `/test-ui`, follow these phases **exactly in order**:

### Phase 0: Prerequisites Check

1. Check if Playwright is installed:
```
powershell -NoProfile -Command "python -c 'import playwright; print(\"OK\")'"
```
If NOT installed, tell Q:
```
Playwright is not installed. To install:
  pip install playwright requests
  playwright install chromium
```
**STOP here if not installed. Do NOT proceed.**

2. Resolve target from arguments:
   - If shorthand (e.g. `flow-builder`), map to full URL using Known Targets table
   - If full URL, use as-is
   - If no target given, ask Q with AskUserQuestion

3. Generate run directory:
```
powershell -NoProfile -Command "cd c:/CRMs/InvektoServices/tools/ui-tester; python -c \"from config import generate_run_dir; print(generate_run_dir('TARGET_NAME'))\""
```
Use the returned path as `RUN_DIR` for all subsequent steps.

### Phase 1: Service Check + Scan

1. **Check service health and auto-start if needed:**
```
powershell -NoProfile -Command "cd c:/CRMs/InvektoServices/tools/ui-tester; python service_manager.py check TARGET_URL"
```
If FAIL: service_manager will attempt auto-start. If still FAIL, tell Q which service needs manual start and **STOP**.

2. **Run scanner:**
```
powershell -NoProfile -Command "cd c:/CRMs/InvektoServices/tools/ui-tester; python scanner.py TARGET_URL RUN_DIR [--auth TENANT:KEY]"
```
Pass `--auth` only if Q provided auth credentials.

3. **Read scan results** using Read tool:
   - Read `RUN_DIR/test-spec.json`
   - Note: Do NOT load screenshots into context. They stay on disk.

### Phase 2: Q Approval

1. **Present scan summary** to Q using AskUserQuestion:

Format:
```
Scan complete:
- X elements discovered (Y buttons, Z links, W inputs)
- Console errors: N
- Network failures: M
- Pages scanned: P

Example test cases:
1. tc-btn-save: "Save Flow" button -> click -> no errors expected
2. tc-input-name: "Flow Name" input -> fill + blur -> no errors expected
3. ...

How to proceed?
```

Options:
- **Approve all** - Run all test cases
- **Errors only** - Test elements with console errors/network failures
- **Manual select** - Q specifies which tests to run
- **Cancel** - Don't test

2. **Update test-spec.json** based on Q's choice:
   - Read the file, set `approved: true` on selected test cases
   - Write updated file back using Write tool

### Phase 3: Test Execution

1. **Run approved tests:**
```
powershell -NoProfile -Command "cd c:/CRMs/InvektoServices/tools/ui-tester; python runner.py RUN_DIR/test-spec.json"
```

2. **Generate HTML report:**
```
powershell -NoProfile -Command "cd c:/CRMs/InvektoServices/tools/ui-tester; python reporter.py RUN_DIR/report.json"
```

3. **Read report** using Read tool:
   - Read `RUN_DIR/report.json`
   - Focus on `summary` and `findings` sections only

### Phase 4: Report to Q

Present findings:

```
## UI Test Report

**Target:** {url}
**Result:** {passed}/{total} PASS ({pass_rate})

### Failed Tests
| Test | Element | Error |
|------|---------|-------|
| tc-btn-X | "Save" button | Console error: ... |
| tc-input-Y | "Name" input | Network failure: 404 |

### Screenshots
Detailed report: RUN_DIR/report.html (open in browser)
Screenshots: RUN_DIR/screenshots/

### What would you like to do?
```

Use AskUserQuestion with options:
- **Fix issues** - Fix the errors (starts normal auto workflow)
- **Retest** - Run the same tests again
- **Done** - Close

If Q chooses "Fix issues":
- List the specific issues
- Q will tell you which ones to fix
- Start normal auto workflow (interview -> plan -> dev -> build -> /rev) for fixes
- After fixes, Q can run `/test-ui` again to verify

---

## Important Rules

1. **Context window protection:** NEVER load screenshots into context. Read only JSON summaries.
2. **File-based communication:** All Playwright output goes to files. Read only structured JSON.
3. **Q owns decisions:** Never auto-fix issues. Always present findings and let Q decide.
4. **No auto workflow integration:** This skill is standalone. No /rev, no Codex review for testing itself.
5. **Windows PowerShell:** All Bash commands must use `powershell -NoProfile -Command "..."` wrapper.
6. **Max 10 pages:** Scanner limits crawling to 10 pages max to prevent infinite loops.
7. **Truncation:** Console errors and network failures truncated to 20 entries max.

## Error Handling

| Error | Action |
|-------|--------|
| Playwright not installed | Show install command, STOP |
| Service not running + auto-start failed | Tell Q which service, show manual start command, STOP |
| Login page + no auth | Ask Q for credentials via AskUserQuestion |
| Scanner timeout (30s) | Report timeout, suggest manual check |
| Element not found during test | Mark FAILED, continue other tests |
| All tests error | Report infrastructure issue, suggest service restart |
