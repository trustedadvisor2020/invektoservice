---
name: automating-workflow
description: Manages the full automated dev workflow including interview, planning, implementation, build, Codex review, and commit. Activates on any code change request without needing to type /auto.
---

# Auto Workflow v5.0 (Paket Bazli Yurutme)

## Overview

> **PERSIST AFTER COMPACT:** This workflow stays active even after session reset.

**This workflow applies automatically to EVERY code change.**
No need to type `/auto` - just tell Q what you need, and the workflow starts.

### v5.0 Farki

- **Paket bazli yurutme:** Birden fazla GR tek pakette islenebilir
- **Tek interview:** Paket scope'unda (tum GR'ler icin), max 4 soru/batch
- **Tek plan:** Paket bazli (`packet_id` + `gr_list` JSON alanlari)
- **Tek Codex review:** Tum GR'lerin diff'i tek review'da
- **Paket ici:** GR'ler arasi interview/review YOK, sadece build check

### After Compact (Session Reset)

After compact, auto workflow **DOES NOT STOP**:
- Pending changes still require `/rev + Codex review`
- Build PASS requirement continues
- Interview gate applies to new tasks

---

## Q's Role

| Step | What Q Does |
|------|-------------|
| 1 | Describe the task (or next packet) |
| 2 | Answer interview questions |
| 3 | Approve the plan |
| 4 | COPY-PASTE bridge (DevAgent <-> Codex) |
| 5 | See result (DONE or escalation) |

Codex trigger: Q's manual copy-paste.

---

## Session Bootstrap

> **Canonical source:** `INVEKTO_BASE.prompt.md` SESSION BOOTSTRAP section.
> Read critical files, start interview, apply PP-006. Details are NOT duplicated here.

---

## Step 0: Q Interview (Mandatory)

**Code MUST NOT be written before completing this step.**

No matter how clear the task seems, interview continues until ALL grey areas are resolved.

**Devil's Advocate (PP-006):** See `INVEKTO_BASE.prompt.md` SEYTANIN AVUKATLIGI section.

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

### Paket Bazli Dev (Phase 2 Farki)

Paket icinde birden fazla GR varsa:
1. GR'leri plan sirasina gore implement et
2. Her GR sonrasi `dotnet build` calistir (build check)
3. Build FAIL -> hemen fix et
4. Tum GR'ler tamamlaninca -> Phase 3 (/rev)
5. GR'ler arasi interview/review YOK

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
Q requests packet (or single task)
    |
Interview via AskUserQuestion (paket scope, max 4 soru/batch)
    |
Plan JSON created (packet_id + gr_list) -> Q approves
    |
GR'ler sirali implement -> build check between GR's
    |
All GR's done -> Final Build PASS
    |
/rev -> Q copy-paste to Codex
    |
Codex produces 2 blocks (entire packet diff)
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