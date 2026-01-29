<!-- VERSION: 1.0 | UPDATED: 2026-01-29 | Persist After Compact | Auto Workflow -->
<!-- COMPACT SONRASI: Auto workflow aktif kalır. Interview → Plan → Dev → Build → /rev → Codex → Commit -->
# INVEKTO MICROSERVICE SYSTEM

Backend microservice altyapısı. .NET 8.0 + Redis + RabbitMQ + Nginx.

## SESSION INIT (CRITICAL - HER SESSION BAŞINDA)

**Her session başladığında (plan modunda bile) şu adımlar OTOMATİK uygulanır:**

1. **Auto Workflow Aktif:** Ne istenirse istensin, auto.md kuralları geçerli
2. **Kritik Dosyaları Oku:**
   - `arch/session-memory.md` → Son durumu anla
   - `arch/active-work.md` → Devam eden işler
   - `arch/lessons-learned.md` → Tekrarlanan hatalar
3. **Interview ile Başla:** Q ne isterse, önce AskUserQuestion ile gri noktaları çöz

**BU ADIMLAR ATLANAMAZ!** Plan mode veya başka mode farketmez.

## Naming & Roles

- The developer is **Q**. Always refer to Q in comments, logs, and explanations.
- You are a coding agent working inside the **InvektoServis** repository.
- When in doubt about requirements or tradeoffs, explicitly ask Q before proceeding.

## Tech Stack

| Component | Stack |
|-----------|-------|
| Microservices | .NET 8.0, ASP.NET Core, Minimal APIs |
| Messaging | RabbitMQ 3.12+ |
| Cache/State | Redis 7.x |
| Gateway | Nginx for Windows |
| Database | SQL Server (Backend owns data) |
| Resilience | Polly (Circuit Breaker, Retry, Bulkhead) |
| Observability | OpenTelemetry, Prometheus, Jaeger, Loki, Grafana |

## Infrastructure

- **Windows-only environment** (dev + production)
- **NO Docker** in production (Windows Services)
- Nginx handles reverse proxy + rate limiting
- Redis for hot path (idempotency, rate limits, cache)
- RabbitMQ for async messaging with retry queues

## Environment Separation (CRITICAL)

**Dev PC and Production Server are DIFFERENT Windows machines!**

| Aspect | Dev PC | Production Server |
|--------|--------|-------------------|
| Machine | Q's local Windows PC | Remote Windows Server |
| OS | **Windows** | **Windows** (NO Linux) |
| Services | Manual `dotnet run` | NSSM Windows Services |
| Path | `c:\CRMs\InvektoServis\` | TBD |

**IMPORTANT:**
- All commands MUST be Windows-based (PowerShell, cmd, .bat)
- NO Linux commands (bash, sh, chmod, etc.)

**Windows PowerShell Rules (CRITICAL):**
- **ALWAYS use PowerShell wrapper for Bash tool:** `powershell -NoProfile -Command "..."`
- NEVER use raw bash/Linux syntax - this is Windows, not Linux
- `&&` chaining does NOT work - use `;` to chain commands
- Example: `powershell -NoProfile -Command "cd c:\path; dotnet build"`

### Port Allocation

| Service | Port |
|---------|------|
| Nginx Gateway | 80 (HTTP), 443 (HTTPS) |
| Chat Analysis | 5001 |
| Admin API | 5010 |
| Redis | 6379 |
| RabbitMQ | 5672, 15672 (management) |
| Prometheus | 9090 |
| Grafana | 3000 |

## Commands

| Task | Command |
|------|---------|
| Build service | `dotnet build src/Invekto.{Service}/` |
| Run service | `dotnet run --project src/Invekto.{Service}/` |
| Run tests | `dotnet test tests/` |

> Auto workflow otomatik uygulanır - `/auto` yazmaya gerek yok.

## Enterprise Code Quality Standards

**MANDATORY for ALL code written in this codebase:**

1. **Enterprise-Grade Quality:** All code must be production-ready.
2. **System Integrity First:** Every change must not break existing functionality.
3. **Rule & Pattern Compliance:** Follow existing codebase patterns and arch/ documentation.
4. **Ask Q When Unclear:** Logic wrong, missing info, multiple approaches → ASK Q
5. **Q Interview (MANDATORY):** Her kod değişikliği öncesi interview yap.
6. **Heavy Load Ready:** System will serve thousands of concurrent users.
7. **User-Friendly Error Messages:** Errors must be specific and actionable.

## Critical Rules

### Source of Truth

- **Master Plan:** `plans/00_master_implementation_plan.md` = TEK GERÇEK KAYNAK
- **Contracts:** `arch/contracts/` = API ve data contract'ları
- **Error Codes:** `arch/errors.md` = Hata kodları

### Key Architecture Principles

1. **Backend = Source of Truth:** Microservices sadece işlem yapar, veri Backend'de kalır
2. **Redis = Hot Path:** Idempotency, rate limiting, cache için
3. **RabbitMQ = Async:** 5 retry queue + DLQ
4. **Polly = Resilience:** Circuit breaker, retry, bulkhead, timeout
5. **No Inter-Service Auth:** Internal network güvenli kabul edilir

### Retry Strategy

| Type | Config |
|------|--------|
| Sync | Max 1 retry, 150ms total overhead |
| Async | 5 retries: 2s → 10s → 30s → 2m → 10m → DLQ |
| Circuit Breaker | 50% failure threshold, 30s break |

## Architecture Reference

**KURAL: Kod yazmadan ÖNCE ilgili `arch/` veya `plans/` dokümanını oku!**

| Yazacağın Kod | Önce Oku |
|---------------|----------|
| Infrastructure | `plans/00_master_implementation_plan.md` Phase 1 |
| Resilience/Polly | `plans/00_master_implementation_plan.md` Phase 2 |
| Messaging/Queue | `plans/00_master_implementation_plan.md` Phase 3 |
| Error handling | `arch/errors.md` |
| API contract | `arch/contracts/` |

All rules in `arch/`:
- `arch/errors.md` - Error codes
- `arch/contracts/` - Data contracts
- `arch/session-memory.md` - Session context
- `arch/active-work.md` - In-progress task tracker
- `arch/lessons-learned.md` - Common mistakes and patterns

## Agent Prompts

Skills in `.claude/commands/`:
- `auto.md` - Default workflow (otomatik uygulanır)
- `rev.md` - Review protocol

## Workflow (v1.0)

> **PERSIST AFTER COMPACT:** Bu workflow session sıfırlansa bile aktif kalır.

**AUTO WORKFLOW = DEFAULT DAVRANIS**

**Her kod değişikliği otomatik olarak auto.md kurallarını takip eder.**

**Otomatik Akış:**
1. Q bir şey ister → AskUserQuestion ile interview
2. Agent risk'i belirler (LOW/MEDIUM/HIGH/CRITICAL)
3. Plan JSON oluşturulur
4. Implement → Build
5. /rev → Q copy-paste → Codex → PASS/FAIL

## Execution

- Execute without interruption for clear tasks
- Read arch/ and plans/ before any task
- If rule conflicts with code, fix code (arch is truth)
- No tests, no docs unless requested

**Execution discipline:**
- Treat any **surprise** as a signal your mental model is wrong.
- If you lose track of the original goal, say so explicitly and reconstruct.

## Ask Before Acting

**MUST ask Q if:**
- Requirements unclear or ambiguous
- Multiple valid approaches exist
- New pattern not in existing codebase
- Changing shared contracts/schemas
- Adding new dependencies
- Modifying auth/security logic

**Proceed directly:**
- Clear instruction = direkt başla, auto workflow otomatik uygulanır
- Q override komutları: `STOP`, `SKIP CODEX`, `FORCE PASS`

## Architecture Compliance

**Before writing code:**
1. Read relevant arch/ and plans/ files
2. Check existing patterns in codebase
3. Verify contract fields exist in arch/contracts/
4. Use error codes from arch/errors.md
5. Never invent new schemas - ask if needed

**Code review checklist:**
- [ ] Uses existing patterns, not new inventions
- [ ] Error codes match arch/errors.md
- [ ] Follows retry/circuit breaker strategy
- [ ] No hardcoded endpoints/ports
- [ ] Idempotency where needed

---

**Full workflow details in `.claude/commands/auto.md` and `agents.md`.**
