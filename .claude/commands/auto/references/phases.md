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
6. **Acceptance Criteria Gate:** Interview sonunda min 2 AC sorusu sor, Q'dan teyit al -> plan.acceptance_criteria'ya yaz
7. Create JSON plan: `arch/plans/{slug}.json` (acceptance_criteria dahil)
8. Write **Verification Questions** (mandatory for ALL risk levels)
9. Write **AHA Moments** (5 suggestions mandatory)
10. Show brief + acceptance criteria to Q, ask "Approve?"

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
5. Build PASS -> **AC Verification:** her acceptance_criteria icin verified=true/false guncelle
6. Tum AC'ler verified=true -> proceed to Phase 3
7. AC unmet -> fix implementation veya Q'ya escalate

**Build PASS != Done.** Build PASS + AC verified -> mandatory Phase 3 (Review).

### Paket Bazli Dev (v5.0)

Paket icinde birden fazla GR varsa:
1. GR'leri plan sirasina gore implement et
2. Her GR sonrasi build check
3. Build FAIL -> hemen fix et
4. Tum GR'ler tamamlaninca -> Phase 3 (/rev)
5. GR'ler arasi interview/review YOK

---

## Phase 3: Review (MCP Automated)

### Risk-Based Trigger

| Risk | Next Step |
|------|-----------|
| LOW | /rev -> MCP codex_review (automated) |
| MEDIUM | /rev -> MCP codex_review (automated) |
| HIGH | /rev -> MCP codex_review (automated) |
| CRITICAL | /rev -> MCP codex_review + wait for Q approval |

**Note:** Codex review is mandatory for ALL risk levels.

### MCP Review Flow (v5.1 - Replaces Copy-Paste)

1. DevAgent runs `/rev`
2. JSON plan file is updated
3. Diff file written: `arch/plans/diffs/{slug}.diff`
4. **Secret scan on diff** (BLOCKING - see rev.md)
5. DevAgent calls `mcp__codex-review__codex_review` with:
   - slug, risk_level, iteration, summary
   - files_changed, git_diff, verification_questions
   - build_status: "PASS"
6. MCP tool calls OpenAI Codex API and returns structured JSON
7. DevAgent processes result:
   - `verdict: "PASS"` -> proceed to commit
   - `verdict: "FAIL"` -> show blocking_issues to Q, enter fix loop
   - `verdict: "UNKNOWN"` -> escalate to Q
8. Show Q concise summary: verdict + blocking issues + summary
9. PASS -> commit | FAIL -> fix (max 3 iterations)

**No manual copy-paste needed.** The entire Codex review is automated via MCP.

### MCP Error Handling

If MCP tool returns `error: true`:
- `AUTH_ERROR` -> Check OPENAI_API_KEY in .mcp.json
- `RATE_LIMIT` -> Wait and retry
- `TIMEOUT` -> Try smaller diff or check network
- `MODEL_ERROR` -> Check CODEX_MODEL env var

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
FAIL verdict -> DevAgent fixes -> Self-Review -> Build -> /rev -> MCP codex_review
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
- Acceptance criteria not all verified=true
- Codex review was not done
- Codex verdict is FAIL

DONE only when: Build PASS + All AC verified=true + Codex PASS -> commit

### After PASS -> /wrap Otomatik Calisir

1. Generate commit message (conventional commit)
2. Secret scan (BLOCKING) -> Commit -> Push
3. Update: `arch/session-memory.md`, `arch/active-work.md`
4. JSON plan: `status`: "DONE"
5. Auto-record lessons to `arch/lessons-learned.md` (no Q approval needed)
6. Generate next-session continuation prompt
7. Inform Q: "DONE - {slug}" + `/clear` oner (3+ paket ise PROAKTIF)

> **`/wrap` otomatik calisir.** Q'nun ayrica `/wrap` yazmasina gerek yok.
> Detaylar: `.claude/commands/wrap.md`
