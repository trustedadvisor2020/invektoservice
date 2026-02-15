# Risk Classification

## Task Risk Levels

| Task Type | Risk | Pre-flight |
|-----------|------|------------|
| Typo fix, comment, log message | **LOW** | Skip all |
| UI-only (layout, text, no logic) | **LOW** | Skip all |
| UI display logic (single file) | **LOW** | Build only |
| Business logic, queries, routing | **MEDIUM** | Scope files |
| Multi-file changes | **MEDIUM** | Scope files only |
| DB schema/query change | **HIGH** | Full check |
| Auth/security touch | **CRITICAL** | Full + Q approval |
| New microservice | **HIGH** | Full check + architecture review |

## Key Principle

Risk determines review intensity, NOT whether review happens.
All risk levels require Codex review.
