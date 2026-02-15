---
name: reviewing-code
description: Prepares Codex review by updating plan JSON and generating diff files. Processes PASS/FAIL/UNKNOWN verdicts after Q relays Codex output. Use after build passes or to submit a review verdict.
---

# /rev - Codex Review Prep (v5.0)

> **PERSIST AFTER COMPACT:** Staged changes require Codex review even after session reset.

Codex is a **reviewer only**. It never implements code. It **DOES NOT modify files** - it only produces text output.

---

## Usage Modes

### 1. `/rev` - After Build PASS (Review Prep)

Runs after build PASS. Updates the JSON plan file.

**Preconditions (Hard Fail):**

1. `git diff --cached --name-status` empty? -> "No staged changes. Run git add first."
2. Plan JSON exists? (`arch/plans/{slug}.json`) -> "Plan file not found."
3. Build PASS evidence? (`build.timestamp`) -> "No build evidence. Run build first."
4. `allowed_files` scope check -> WARN if diff contains files outside allowed_files

**JSON Updates:**

```json
{
  "status": "REVIEW",
  "git_diff": {
    "patch_truncated": "First 51200 bytes",
    "sha256": "Hash of full diff",
    "full_path": "arch/plans/diffs/{slug}.diff",
    "stats": { "insertions": 42, "deletions": 10, "files_count": 3 }
  },
  "files_changed": [{ "path": "src/Invekto.AgentAI/Services/ReplyGenerator.cs", "is_new": false }],
  "updated_at": "2026-02-15T12:00:00Z"
}
```

**Diff stats:** Exclude self-referencing diff file:
```bash
git diff --cached --stat -- ':!arch/plans/diffs/*'
```

**Diff file:** `arch/plans/diffs/{slug}.diff` -> Full untruncated diff

**Secret scan (BLOCKING):** Before writing diff file:
```bash
grep -iE 'sk-|apikey.*[a-zA-Z0-9]{20}|password\s*[:=]' diff_file
```
If match found -> WARN Q, do NOT proceed until resolved.

**Prompt shown to Q:**

```
{slug-name} ---
# CODEX REVIEW REQUEST
Plan: arch/plans/{slug}.json
{RISK} :{iteration}
{plan.summary}
```

---

### 2. `/rev validate` - Validation Only (Optional)

Runs validation without updating JSON.

```
/rev validate -> Schema validation -> Coverage check -> Preconditions -> PASS/FAIL report
```

---

### 3. `/rev verdict <PASS|FAIL|UNKNOWN> [issue]` - Verdict Processing

Used when Q relays Codex output to DevAgent.

```bash
/rev verdict PASS
/rev verdict FAIL "CQ2 failed: silent failure in catch block"
/rev verdict UNKNOWN
```

**Rule: FAIL requires an issue description.**
- `/rev verdict FAIL` -> ERROR: blocking_issues cannot be empty
- `/rev verdict FAIL "CQ2 failed: ..."` -> OK

**Iteration increment:** Only incremented on `/rev verdict` calls, not on `/rev` (review prep).

**Escalation trigger:** When iteration >= 3:
- `"escalation_required": true` added to JSON
- DevAgent stops fixing, Q decision required

---

## Devil's Advocate (PP-006)

> **Canonical source:** `INVEKTO_BASE.prompt.md` SEYTANIN AVUKATLIGI section.

During review, challenge the code - don't rubber-stamp:
- Question if the diff is truly minimal
- Flag silent failures and missing error handling
- Ask "what breaks if this input is unexpected?"
- Challenge scope creep ("is this change in the plan?")
- Don't accept PASS just because the build passed

---

## Flow Summary

```
DevAgent /rev -> JSON updated, diff written
    |
Q shown: "Codex review: arch/plans/{slug}.json"
    |
Q pastes prompt to Codex
    |
Codex produces 2 blocks (DOES NOT modify files)
    |
Q relays Codex output to DevAgent
    |
DevAgent /rev verdict PASS|FAIL|UNKNOWN
    |
PASS -> commit -> DONE
FAIL -> fix -> /rev (max 3 iterations)
UNKNOWN -> Q escalation
```

---

## Critical Rules

1. **Codex DOES NOT modify files** - it reads JSON, produces 2 text blocks
2. **Who fills verdict fields?** - DevAgent (based on Q's input)
3. **FAIL + empty blocking_issues = ERROR** - issue description is mandatory
4. **Iteration reaches 3 -> Q escalation** - no new iteration without Q permission
5. **Scope violation = HARD FAIL** - changes outside allowed_files are rejected
6. **Secret in diff = BLOCKING** - scan diff for secrets before proceeding

---

## Codex Output Format

For the expected Codex output format (Code Quality Gate + CoVe Verification blocks), see [references/codex-output-format.md](rev/references/codex-output-format.md).

---

## Q Override

| Command | Effect |
|---------|--------|
| `STOP` | Halt all operations |
| `SKIP CODEX` | Skip /rev, commit directly (Q permission only) |
| `FORCE PASS` | Override verdict (Q permission only) |

---

## Canonical Rule

Codex enforces correctness. DevAgent implements + runs /rev. Q owns decisions + copy-paste bridge.

This rule overrides convenience and speed.
