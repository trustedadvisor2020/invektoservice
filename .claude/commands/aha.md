---
name: generating-aha-moments
description: Generates high-impact "aha moment" UX enhancements for features. Use after a feature feels flat, during planning to inject real value, when reviewing old features for leverage, or when asking "what actually makes this powerful?"
---

# /aha [feature]

Generate **brutally practical "aha moment" enhancements** for the feature discussed in the current session.

---

## Rules (Non-negotiable)

* Every suggestion MUST start from a **real user pain**
* No vague UX language allowed
* Suggestions must be **measurable**
* Must be feasible with a typical SaaS stack
* Prefer workflow-level wins over cosmetic tweaks

Forbidden phrases (if used -> regenerate):
"Improve UX", "More intuitive", "Better experience", "User friendly", "Cleaner interface", "Easier to use"

---

## Devil's Advocate (PP-006)

Challenge the feature before suggesting enhancements:
- "Does this feature actually solve a real problem?"
- "Is the current workflow wrong, or just unfamiliar?"
- "Would removing this feature be better than improving it?"
- Push back on vanity metrics and cosmetic fixes
- Don't suggest what Q wants to hear - suggest what users need

---

## Step 1: Feature & Workflow Context

Identify:

1. Feature/component name
2. Primary job-to-be-done
3. User role (Admin / Agent / User)
4. Full workflow (before -> during -> after)
5. Where users hesitate, repeat actions, or make mistakes
6. What happens immediately before and after this feature

---

## Step 2: Generate Aha Moment Suggestions

For each suggestion:

```md
### [NUMBER]. [Short, concrete title]

**Category:** Performance | UI | Smart Defaults | Power User | Insights | Proactive

**User Pain:** [Specific frustration or inefficiency]

**What:** [Concrete change]

**Why it's an Aha:** [Exact moment the user realizes value]

**Success Metric:**
- Before: [baseline]
- After: [expected improvement]

**Implementation hint:**
- Layer: Frontend | Backend | DB | Infra
- Complexity: Low | Medium | High
- Risk: None | UX | Data | Performance
```

### Example Input/Output

**Input:** "WhatsApp message list - agents browse conversations"

**Output:**

```
### 1. Unread-First Smart Sort

**Category:** Smart Defaults

**User Pain:** Agent scrolls past 50 read messages to find 3 unread ones every morning

**What:** Default sort = unread first + most recent. One-click toggle to chronological.

**Why it's an Aha:** Agent opens the list and sees exactly what needs attention - zero scrolling.

**Success Metric:**
- Before: 12 seconds average to find first unread
- After: 0 seconds - it's already on top

**Implementation hint:**
- Layer: Backend (sort query) + Frontend (toggle)
- Complexity: Low
- Risk: None
```

---

## Step 3: Prioritize

| Priority        | Effort | Impact | Reason |
| --------------- | ------ | ------ | ------ |
| Quick Win       | Low    | High   |        |
| Good Investment | Medium | High   |        |
| Nice to Have    | Low    | Medium |        |
| Future          | High   | High   |        |

---

## Kill List (Do NOT Build)

List 1-3 ideas that look attractive but **should NOT be implemented**, and explain why they are distractions, overkill, or premature.

---

## Q's Pick

Select what to build:

> Example: "1, 3" or "all quick wins"

---

## References

See [aha/references/feature-ideas.md](aha/references/feature-ideas.md) for example ideas by feature type.
