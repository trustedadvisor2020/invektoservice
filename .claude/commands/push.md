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

### Step 3: Safety Check

Before staging, verify no sensitive files are included:
- `.env`, `.env.*` files
- `*credentials*`, `*secret*`, `*token*` files
- `appsettings.Production.*.json` with real secrets
- Private keys (`*.pem`, `*.key`)

If sensitive files detected -> warn Q and exclude them from staging.

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
| Push rejected (non-fast-forward) | Warn Q: "Remote has new commits. Run `git pull` first." Do NOT force push |
| No remote configured | Warn Q: "No remote 'origin' found." STOP |
| Auth failure | Warn Q: "Authentication failed. Check credentials." STOP |
| Pre-commit hook failure | Show hook output, suggest fix, do NOT use --no-verify |

## Notes

- Always pushes to current branch
- Never force pushes
- Skips if no changes detected
- Checks for sensitive files before staging
