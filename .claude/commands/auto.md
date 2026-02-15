---
name: automating-workflow
description: Manages the full automated dev workflow including interview, planning, implementation, build, Codex review, and commit. Activates on any code change request without needing to type /auto.
---

# Auto Workflow v3.1

## Overview

> **PERSIST AFTER COMPACT:** This workflow stays active even after session reset.

**This workflow applies automatically to EVERY code change.**
No need to type `/auto` - just tell Q what you need, and the workflow starts.

### After Compact (Session Reset)

After compact, auto workflow **DOES NOT STOP**:
- Pending changes still require `/rev + Codex review`
- Build PASS requirement continues
- Interview gate applies to new tasks

---

## Q's Role

| Step | What Q Does |
|------|-------------|
| 1 | Describe the task |
| 2 | Answer interview questions |
| 3 | Approve the plan |
| 4 | COPY-PASTE bridge (DevAgent <-> Codex) |
| 5 | See result (DONE or escalation) |

Codex trigger: Q's manual copy-paste.

---

## Session Bootstrap (Every Session Start)

1. **Read critical files:**
   - `arch/session-memory.md` -> Current state
   - `arch/active-work.md` -> In-progress work
   - `arch/lessons-learned.md` -> Recurring mistakes
   - `INVEKTO_BASE.prompt.md` -> Global rules
2. Q makes a request -> Start interview (Step 0)
3. Normal auto workflow continues

---

## Step 0: Q Interview (Mandatory)

**Code MUST NOT be written before completing this step.**

### Core Rule

No matter how clear the task seems, interview continues until ALL grey areas are resolved.
- "Seems clear" != "No need to ask"
- Every assumption = potential wrong direction

### Devil's Advocate (PP-006 - Mandatory)

**DO:**
- Offer alternative approaches
- Ask about edge cases ("What if X happens?")
- Point out potential risks
- Discuss trade-offs
- Question Q's assumptions

**DON'T:**
- Accept Q's first answer and move on
- Say "understood" and dive into code
- Hesitate to ask tough questions

Goal: Make Q think better, NOT be a yes-man.

### Interview Flow

1. Ask questions via AskUserQuestion tool (max 4 questions/batch)
2. Q answers
3. New grey area from answer? -> Ask via AskUserQuestion
4. All grey areas resolved? -> If no, go to 3
5. Summarize, ask "Approve?"
6. Q approves -> Go to Phase 1

**Required:** Interview questions MUST use AskUserQuestion tool.

---

## Workflow Phases

For detailed phase instructions, see the reference files:

- **Phase 1 - Plan:** See [references/phases.md](auto/references/phases.md#phase-1-plan)
- **Phase 2 - Dev:** See [references/phases.md](auto/references/phases.md#phase-2-dev)
- **Phase 3 - Review:** See [references/phases.md](auto/references/phases.md#phase-3-review)
- **Phase 4 - Fix-Run:** See [references/phases.md](auto/references/phases.md#phase-4-fix-run)
- **Phase 5 - Done:** See [references/phases.md](auto/references/phases.md#phase-5-done)

### Risk Classification

See [references/risk-classification.md](auto/references/risk-classification.md) for the full risk table.

Quick reference:

| Risk | Examples | Post-Build |
|------|----------|------------|
| LOW | Typo, comment, log, UI text | /rev -> Codex |
| MEDIUM | Business logic, queries, routing | /rev -> Codex |
| HIGH | DB schema, multi-file | /rev -> Codex |
| CRITICAL | Auth/security | /rev + Q approval |

---

## Summary Flow

```
Q requests something
    |
Interview via AskUserQuestion
    |
Plan JSON created -> Q approves
    |
Code written -> Build PASS
    |
/rev -> Q copy-paste to Codex
    |
Codex produces 2 blocks
    |
Q reports verdict
    |
/rev verdict PASS|FAIL
    |
PASS -> commit -> DONE
FAIL -> fix -> /rev (max 3 iterations)
```

---

## Q Override

| Command | Effect |
|---------|--------|
| `STOP` | Halt all operations |
| `SKIP CODEX` | Skip Codex review (Q permission only) |
| `FORCE PASS` | Override verdict (Q permission only) |
| `ROLLBACK` | Revert last changes |