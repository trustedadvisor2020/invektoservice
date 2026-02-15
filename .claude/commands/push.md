---
name: pushing-changes
description: Stages all changes, generates a conventional commit message, and pushes to the current branch on GitHub. Use when ready to push completed work.
---

# /push [message]

> Quick command to push all changes to GitHub with auto-generated commit message.

## Usage

```
/push                    # Auto-generate commit message from changes
/push "fix: bug fix"     # Use custom commit message
```

## Devil's Advocate (PP-006)

> **Canonical source:** `INVEKTO_BASE.prompt.md` SEYTANIN AVUKATLIGI section.

Challenge before pushing:
- "Are there uncommitted changes that should be in this push?"
- "Is the commit message accurate about what actually changed?"
- "Should this go to a feature branch instead of master?"
- Don't push just because Q said so - flag concerns first

---

## Workflow

### Step 1: Check Status

Run in parallel:
- `git status` - See all changed files
- `git diff --stat` - See change statistics
- `git log -3 --oneline` - See recent commits for style reference

### Step 2: Analyze Changes

Determine:
- Change type (feat, fix, refactor, docs, chore)
- Which components/services affected
- Brief summary of what was done

### Step 3: Secret Scan (BLOCKING GATE)

**Before staging, scan ALL changed files for secrets:**

```bash
powershell -NoProfile -Command "git diff --name-only | ForEach-Object { Select-String -Path $_ -Pattern 'sk-|apikey|password\s*[:=]|secret\s*[:=]|-----BEGIN' -AllMatches }"
```

**Also check these high-risk patterns:**
- `.env`, `.env.*` files
- `*credentials*`, `*secret*`, `*token*` files
- `appsettings.Production.*.json` with real secrets (not placeholders)
- Private keys (`*.pem`, `*.key`)
- `deploy_output/**/appsettings.json` (build output copies dev secrets)

**If ANY secret detected -> HARD STOP:**
1. List the files and matched patterns
2. Do NOT stage or commit
3. Ask Q how to proceed (placeholder replacement, .gitignore, etc.)

**This is a BLOCKING GATE, not a warning.** Push CANNOT proceed until resolved.

### Step 4: Stage Changes

```bash
git add -A
```

If sensitive files were detected in Step 3, stage specific files instead of `-A`.

### Step 5: Create Commit

If `[message]` argument provided, use it. Otherwise auto-generate from changes.

Commit message format:
```
{type}({scope}): {brief description}

- {detail 1}
- {detail 2}

Co-Authored-By: Claude <noreply@anthropic.com>
```

Types: `feat`, `fix`, `refactor`, `docs`, `chore`

### Step 6: Push to Remote

```bash
git push origin {current_branch}
```

### Step 7: Confirm

Show:
- Commit hash
- Branch name
- Files changed count
- Push status

## Error Handling

| Error | Action |
|-------|--------|
| No changes detected | Report "Nothing to commit", STOP |
| Secret detected | HARD STOP - list files, ask Q (Step 3) |
| Push rejected (non-fast-forward) | Warn Q: "Remote has new commits. Run `git pull` first." Do NOT force push |
| No remote configured | Warn Q: "No remote 'origin' found." STOP |
| Auth failure | Warn Q: "Authentication failed. Check credentials." STOP |
| Pre-commit hook failure | Show hook output, suggest fix, do NOT use --no-verify |
| GitHub Push Protection | Soft reset, replace secret with placeholder, recommit |

## Notes

- Always pushes to current branch
- Never force pushes
- Skips if no changes detected
- **Secret scan is BLOCKING** - not a soft warning
- deploy_output/ is a known secret leak vector (3 incidents: 2026-02-09, 2026-02-15)