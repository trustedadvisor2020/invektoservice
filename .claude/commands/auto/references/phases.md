# Workflow Phases

Detailed instructions for each phase of the auto workflow.

---

## Pre-Flight Check (Risk-Based)

### Mandatory Reads (Always)

Read these files BEFORE writing code:

- `arch/session-memory.md`
- `arch/active-work.md`
- `arch/lessons-learned.md`
- `arch/contracts/`
- `arch/errors.md`
- `INVEKTO_BASE.prompt.md`

---

## Phase 1: Plan

**Devil's Advocate checkpoint:** Before planning, challenge the approach:
- "Is there a simpler way to achieve this?"
- "What existing code/pattern already solves part of this?"
- "What's the worst thing that happens if we don't build this?"

1. Generate slug: `YYYYMMDD-feature-name`
2. Analyze codebase (patterns, conventions)
3. Determine scope from `arch/contracts/*`
4. Identify risks, affected modules/services
5. **Read `arch/contracts/plan-schema.json` BEFORE creating plan JSON**
6. Create JSON plan: `arch/plans/{slug}.json`
7. Write **Verification Questions** (mandatory for ALL risk levels)
8. Write **AHA Moments** (5 suggestions mandatory)
9. Show brief to Q, ask "Approve?"

### AHA Checklist (5 suggestions mandatory)

Each suggestion must include:
- **Category:** UX | SPEED | RELIABILITY | SALES | SUPPORT
- **User Pain:** Concrete user problem
- **Suggestion:** What to do (1 sentence)
- **Aha Moment:** When the user says "wow!"

### Approval Gate (Hard Stop)

- Q MUST say "approve" / "ok" / "yes" / "continue" to proceed
- Without explicit approval -> DO NOT proceed to Phase 2

---

## Phase 2: Dev

1. Implement code (max 3 steps per batch)
2. **Self-Review** (CQ1-8 + AQ1-6) after each file edit
3. **BUILD immediately** after each file edit:
   ```bash
   # Full solution build
   powershell -NoProfile -Command "dotnet build C:\CRMs\InvektoServices\InvektoServis.sln --no-restore -v q"

   # Single service build
   powershell -NoProfile -Command "dotnet build C:\CRMs\InvektoServices\src\Invekto.{Name}\Invekto.{Name}.csproj --no-restore -v q"
   ```
4. If build fails -> fix immediately
5. Build PASS -> proceed to Phase 3

**Build PASS != Done.** Build PASS -> mandatory Phase 3 (Review).

### Paket Bazli Dev (v5.0)

Paket icinde birden fazla GR varsa:
1. GR'leri plan sirasina gore implement et
2. Her GR sonrasi build check
3. Build FAIL -> hemen fix et
4. Tum GR'ler tamamlaninca -> Phase 3 (/rev)
5. GR'ler arasi interview/review YOK

---

## Phase 3: Review (Copy-Paste)

### Risk-Based Trigger

| Risk | Next Step |
|------|-----------|
| LOW | /rev -> Q copy-paste -> Codex |
| MEDIUM | /rev -> Q copy-paste -> Codex |
| HIGH | /rev -> Q copy-paste -> Codex |
| CRITICAL | /rev + wait for Q approval |

**Note:** Codex review is mandatory for ALL risk levels.

### Copy-Paste Review Flow

1. DevAgent runs `/rev`
2. JSON plan file is updated
3. Diff file written: `arch/plans/diffs/{slug}.diff`
4. **Secret scan on diff** (BLOCKING - see rev.md)
5. Codex prompt shown to Q:
   ```
   {slug-name} ---
   # CODEX REVIEW REQUEST
   Plan: arch/plans/{slug}.json
   {RISK} :{iteration}
   {plan.summary}

   ## Verification Questions
   - [ ] {Q1.category}: {Q1.question}
   - [ ] {Q2.category}: {Q2.question}
   - [ ] {Q3.category}: {Q3.question}
   ```
6. Q pastes to Codex window
7. Codex produces 2 review blocks
8. Q reports Codex output to DevAgent
9. DevAgent runs `/rev verdict PASS|FAIL` to update JSON
10. PASS -> commit | FAIL -> fix (max 3 iterations)

**Hard Rule:** After /rev, commit is NEVER allowed until Codex prompt is shown to Q.

---

## Phase 4: Fix-Run (After FAIL)

### Iteration Limits

| Risk | Max Iterations | Escalation |
|------|----------------|------------|
| LOW | 3 | Inform Q |
| MEDIUM | 3 | Escalate to Q |
| HIGH+ | 3 | Q approval required |

### Fix Loop

```
FAIL verdict -> DevAgent fixes -> Self-Review -> Build -> /rev -> Q copy-paste -> Codex
-> PASS -> commit | FAIL -> fix again (iteration++)
```

### Escalation Categories

| Category | Meaning |
|----------|---------|
| DECISION_CONFLICT | Design decision needed, not a bug |
| TOOL_LIMITATION | Tool/framework limitation |
| PLAN_ASSUMPTION_WRONG | Plan assumption was incorrect |
| SCOPE_INSUFFICIENT | Plan scope is too narrow |
| ARCHITECTURE_CONFLICT | Conflicts with existing architecture |

---

## Phase 5: Done (After PASS)

### Done Gate

Cannot mark as DONE if:
- Plan JSON was not created
- Codex review was not done
- Codex verdict is FAIL

DONE only when: Build PASS + Codex PASS -> commit

### After PASS

1. Generate commit message (conventional commit)
2. Commit to work branch
3. Merge to master
4. Update: `arch/session-memory.md`, `arch/active-work.md`
5. JSON plan: `status`: "DONE"
6. Inform Q: "DONE - {slug}"
7. Auto-record lessons to `arch/lessons-learned.md` (no Q approval needed)