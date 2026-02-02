---
description: Git add, commit and push all changes to GitHub
---

# /push [message]

> Quick command to push all changes to GitHub with auto-generated commit message.

## Usage

```
/push                    # Auto-generate commit message from changes
/push "fix: bug fix"     # Use custom commit message
```

## Workflow

### Step 1: Check Status
Run these commands in parallel:
- `git status` - See all changed files
- `git diff --stat` - See change statistics
- `git log -3 --oneline` - See recent commits for style reference

### Step 2: Analyze Changes
Look at the changes and determine:
- What type of change (feat, fix, refactor, docs)
- Which components/services affected
- Brief summary of what was done

### Step 3: Stage All Changes
```
git add -A
```

### Step 4: Create Commit
If `[message]` argument provided, use it. Otherwise auto-generate from changes.

Commit message format:
```
{type}: {brief description}

- {detail 1}
- {detail 2}

Co-Authored-By: Claude <noreply@anthropic.com>
```

Types:
- `feat` - New feature
- `fix` - Bug fix
- `refactor` - Code refactoring
- `docs` - Documentation
- `chore` - Maintenance tasks

### Step 5: Push to Remote
```
git push origin {current_branch}
```

### Step 6: Confirm
Show:
- Commit hash
- Branch
- Files changed count
- Push status

## Example Output

```
✓ Commit: abc1234
✓ Branch: master
✓ Files: 12 changed
✓ Pushed to origin/master
```

## Notes
- Always pushes to current branch
- Never force push
- Skip if no changes detected
