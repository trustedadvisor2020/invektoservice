---
name: generating-session-prompt
description: Generates a continuation prompt for the next session. ONLY outputs prompt text - no file reading, no implementation, no exploration.
---

# /session-prompt

> Generate a next-session continuation prompt from current context. Nothing else.

## CRITICAL RULES

1. **DO NOT** re-read files, explore codebase, or start implementation
2. **DO NOT** ask questions or begin any workflow
3. **ONLY** output the prompt text block below
4. Use what you already know from this session's context

---

## Output Format

```markdown
## Session Continuation Prompt

**PROJECT:** InvektoServis | Multi-tenant SaaS
**DATE:** {today}
**PHASE:** {current phase/packet from context}

### COMPLETED THIS SESSION
- {bullet list of what was done}

### CURRENT STATUS
- {status summary - what's done, what's in progress}

### NEXT STEPS (Priority Order)
1. {next task with enough detail to resume}
2. {following task}
3. {etc.}

### KEY DECISIONS MADE
- {architectural or design decisions from this session}

### KNOWN ISSUES / BLOCKERS
- {any blockers, or "None"}

### FILES TOUCHED
- {list of key files modified this session}

### RESUME INSTRUCTIONS
> 1. Read `arch/session-memory.md` and `arch/active-work.md`
> 2. Read `arch/lessons-learned.md` for recent patterns
> 3. Proceed with: {specific next task}
```

---

## Notes

- This is a **zero-side-effect** command - it produces text output only
- If context is insufficient, output what you know and mark gaps as `[UNKNOWN]`
- Never trigger auto workflow, interview, or planning from this command
