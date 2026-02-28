---
name: wrapping-phase
description: Consolidates post-phase housekeeping into one command. Updates tracking docs, records lessons, commits, pushes, and generates next-session prompt.
---

# /wrap-phase [message]

> One command to close out a completed phase/packet and prepare for the next session.

## Usage

```
/wrap-phase                     # Full wrap-up with auto commit message
/wrap-phase "pkt-4 done"        # Custom commit message
```

## Devil's Advocate (PP-006)

> **Canonical source:** `INVEKTO_BASE.prompt.md` SEYTANIN AVUKATLIGI section.

Challenge before wrapping:
- "Is this phase actually done, or are there loose ends?"
- "Did the last Codex review PASS, or are we wrapping with known issues?"
- "Are there uncommitted changes that belong to a different phase?"
- Don't wrap just to move on - ensure quality gates passed

---

## Workflow (5 Steps - Sequential)

### Step 1: Tracking Doc Updates

Update these files to reflect completed work:

1. **`arch/session-memory.md`** - Update Last Task, Next Task, Status
2. **`arch/active-work.md`** - Mark completed items, update Execution Queue

Read each file first, then update only the relevant sections.

### Step 2: Record Lessons (/learn Auto Mode)

Scan the session for learnable signals (same logic as `/learn`):

1. Q corrections, recurring patterns, approved approaches, error->solution chains
2. Filter: project-specific + repeatable + new + actionable (3/4 rule)
3. Append directly to `arch/lessons-learned.md` (auto mode, no Q approval needed)
4. Cross-project lesson ise `C:\Users\taner\.claude\workflow\shared-lessons.md`'ye de ekle
5. If nothing learnable found, skip silently

### Step 2.5: Git Hygiene Scan

Check for files that should NOT be tracked (already in `.gitignore` but previously committed):

```bash
powershell -NoProfile -Command "git -C C:\CRMs\InvektoServices ls-files --cached -i --exclude-standard | Measure-Object -Line"
```

**If count > 0:** Remove from tracking with `git rm -r --cached <path>` and stage the removal.
Common offenders: `node_modules/`, `deploy_output/`, `*.pfx`, `appsettings.Production.json`.

### Step 3: Secret Scan (BLOCKING GATE)

**Same as /push Step 3 - scan ALL changed files for secrets.**

```bash
powershell -NoProfile -Command "git -C C:\CRMs\InvektoServices diff --name-only | ForEach-Object { Select-String -Path $_ -Pattern 'sk-|apikey|password\s*[:=]|secret\s*[:=]|-----BEGIN' -AllMatches }"
```

**If ANY secret detected -> HARD STOP.** Do not proceed to commit.

### Step 4: Commit & Push

1. `git add -A` (or specific files if secrets were near-miss)
2. Generate conventional commit message from changes:
   ```
   chore(phase): {brief description}

   - {tracking updates}
   - {lessons recorded}

   Co-Authored-By: Claude <noreply@anthropic.com>
   ```
3. `git push origin {current_branch}`

If `[message]` argument provided, use it as commit message instead.

### Step 5: Generate Next-Session Prompt

**Output ONLY the prompt text.** Do NOT re-read files or start new work.

Format:
```markdown
## Next Session Continuation

**CONTEXT:** {project name} | {current phase/packet}
**LAST COMPLETED:** {what was done this session}
**STATUS:** {current status from session-memory.md}

### NEXT STEPS
- {ordered list of next tasks}

### KEY DECISIONS
- {decisions made this session that affect future work}

### BLOCKERS
- {any known issues or blockers, or "None"}

### RESUME COMMAND
> Start with: Read `arch/session-memory.md` and `arch/active-work.md`, then proceed with {next task}.
```

---

## Error Handling

| Error | Action |
|-------|--------|
| No changes to commit | Skip Steps 3-4, still do Steps 1-2 and 5 |
| Secret detected | HARD STOP at Step 3 |
| Push rejected | Warn Q, do NOT force push |
| No lessons found | Skip Step 2 silently |
| Tracking files missing | Warn Q, create if appropriate |

## Notes

- This skill chains /learn (auto) + /push into one flow
- Tracking updates happen BEFORE commit so they're included
- Next-session prompt is output AFTER push (not committed)
- Never force push, never skip secret scan
