# Architecture Documentation

> Single source of truth for Invekto Microservice System architecture.

## Structure

```
arch/
├── README.md               # This file
├── session-memory.md       # Session context (where we left off)
├── active-work.md          # In-progress task tracker
├── lessons-learned.md      # Common mistakes and patterns
├── errors.md               # Error codes registry
│
├── contracts/              # Data contracts
│   ├── standard-response.json
│   └── plan-schema.json
│
└── plans/                  # Feature implementation plans
    └── diffs/              # Git diffs for review
```

## Key Documents

| Document | Purpose |
|----------|---------|
| `session-memory.md` | Session continuity - last task, decisions |
| `active-work.md` | Track in-progress work, blockers |
| `lessons-learned.md` | Patterns and anti-patterns |
| `errors.md` | Standard error codes |

## Reading Order (Per Session)

1. `session-memory.md` - What happened last time?
2. `active-work.md` - Any pending tasks?
3. `lessons-learned.md` - Patterns to remember
4. Relevant `contracts/` for the task
5. Master plan: `plans/00_master_implementation_plan.md`

## Contracts

JSON Schema files defining API contracts:
- `standard-response.json` - Standard API response format
- `plan-schema.json` - Implementation plan format

## Plans

Feature implementation plans in JSON format:
- Named: `YYYYMMDD-feature-name.json`
- Diffs stored in: `plans/diffs/`

## Rules

1. **arch/ = Truth** - If code conflicts with arch/, fix the code
2. **Read before write** - Always read relevant docs before coding
3. **Update after done** - Keep session-memory and active-work current
4. **Learn from mistakes** - Add new patterns to lessons-learned

---

**Maintained by:** Q + Claude
**Last Updated:** 2026-01-29
