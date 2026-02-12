---
description: Semi-autonomous UI testing - scan localhost UIs with Playwright, generate test specs for Q approval, execute approved tests, report findings
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

## EXECUTION PROTOCOL

When Q runs `/test-ui`, follow these phases **exactly in order**:

### PHASE 0: PREREQUISITES CHECK

1. Check if Playwright is installed:
```
powershell -NoProfile -Command "python -c 'import playwright; print(\"OK\")'"
```
If NOT installed, tell Q:
```
Playwright kurulu degil. Kurmak icin:
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
powershell -NoProfile -Command "cd c:\CRMs\InvektoServices\tools\ui-tester; python -c \"from config import generate_run_dir; print(generate_run_dir('TARGET_NAME'))\""
```
Use the returned path as `RUN_DIR` for all subsequent steps.

### PHASE 1: SERVICE CHECK + SCAN

1. **Check service health and auto-start if needed:**
```
powershell -NoProfile -Command "cd c:\CRMs\InvektoServices\tools\ui-tester; python service_manager.py check TARGET_URL"
```
If FAIL: service_manager will attempt auto-start. If still FAIL, tell Q which service needs manual start and **STOP**.

2. **Run scanner:**
```
powershell -NoProfile -Command "cd c:\CRMs\InvektoServices\tools\ui-tester; python scanner.py TARGET_URL RUN_DIR [--auth TENANT:KEY]"
```
Pass `--auth` only if Q provided auth credentials.

3. **Read scan results** using Read tool:
   - Read `RUN_DIR/test-spec.json`
   - Note: Do NOT load screenshots into context. They stay on disk.

### PHASE 2: Q APPROVAL

1. **Present scan summary** to Q using AskUserQuestion:

Format your question like this:
```
Scan tamamlandi:
- X element kesfedildi (Y button, Z link, W input)
- Console errors: N adet
- Network failures: M adet
- Sayfalar tarandi: P

Ornek test case'ler:
1. tc-btn-save: "Save Flow" butonu -> click -> no errors expected
2. tc-input-name: "Flow Name" input -> fill + blur -> no errors expected
3. ...

Nasil devam edelim?
```

Options:
- **Hepsini onayla** - Tum test case'leri calistir
- **Sadece hatalilari test et** - Console error/network failure olan elementleri test et
- **Manuel sec** - Q hangi testleri istedigini belirtir
- **Iptal** - Test yapma

2. **Update test-spec.json** based on Q's choice:
   - Read the file, set `approved: true` on selected test cases
   - Write updated file back using Write tool

### PHASE 3: TEST EXECUTION

1. **Run approved tests:**
```
powershell -NoProfile -Command "cd c:\CRMs\InvektoServices\tools\ui-tester; python runner.py RUN_DIR\test-spec.json"
```

2. **Generate HTML report:**
```
powershell -NoProfile -Command "cd c:\CRMs\InvektoServices\tools\ui-tester; python reporter.py RUN_DIR\report.json"
```

3. **Read report** using Read tool:
   - Read `RUN_DIR/report.json`
   - Focus on `summary` and `findings` sections only

### PHASE 4: REPORT TO Q

Present findings to Q in this format:

```
## UI Test Raporu

**Target:** {url}
**Sonuc:** {passed}/{total} PASS ({pass_rate})

### Basarisiz Testler
| Test | Element | Hata |
|------|---------|------|
| tc-btn-X | "Save" button | Console error: ... |
| tc-input-Y | "Name" input | Network failure: 404 |

### Screenshots
Detayli rapor: RUN_DIR/report.html (browser'da ac)
Screenshots: RUN_DIR/screenshots/

### Ne yapmak istersin?
```

Use AskUserQuestion with options:
- **Fix issues** - Hatalari duzelt (normal auto workflow baslat)
- **Tekrar test et** - Ayni testleri tekrar calistir
- **Bitti** - Kapat

If Q chooses "Fix issues":
- List the specific issues
- Q will tell you which ones to fix
- Start normal auto workflow (interview -> plan -> dev -> build -> /rev) for fixes
- After fixes, Q can run `/test-ui` again to verify

---

## IMPORTANT RULES

1. **Context window protection:** NEVER load screenshots into context. Read only JSON summaries.
2. **File-based communication:** All Playwright output goes to files. Read only structured JSON.
3. **Q owns decisions:** Never auto-fix issues. Always present findings and let Q decide.
4. **No auto workflow integration:** This skill is standalone. No /rev, no Codex review for the testing itself.
5. **Windows PowerShell:** All Bash commands must use `powershell -NoProfile -Command "..."` wrapper.
6. **Max 10 pages:** Scanner limits crawling to 10 pages max to prevent infinite loops.
7. **Truncation:** Console errors and network failures truncated to 20 entries max.

## ERROR HANDLING

| Error | Action |
|-------|--------|
| Playwright not installed | Show install command, STOP |
| Service not running + auto-start failed | Tell Q which service, show manual start command, STOP |
| Login page + no auth | Ask Q for credentials via AskUserQuestion |
| Scanner timeout (30s) | Report timeout, suggest manual check |
| Element not found during test | Mark FAILED, continue other tests |
| All tests error | Report infrastructure issue, suggest service restart |
