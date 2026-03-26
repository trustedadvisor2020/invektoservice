## INVEKTOSERVICES PROJECT CONTEXT
- Multi-tenant SaaS microservices platform for WhatsApp-based customer engagement
- Runtime: .NET 8 (C#), Minimal API pattern
- Database: PostgreSQL 16 + pgvector (NEVER SQL Server, NEVER SQLite)
- Frontend: React 18 + TypeScript + Vite (Dashboard + FlowBuilder SPA)
- Shared library: Invekto.Shared (DTOs, constants, utilities, auth, middleware)
- Architecture: Independent microservices, each with own port, own DB tables, own deploy
- Services: Backend (:5000), ChatAnalysis (:7101), Appointments (:7102), Knowledge (:7104), AgentAI (:7105), Integrations (:7106), Outbound (:7107), Automation (:7108), WhatsAppAnalytics (:7109)
- Auth: JWT (HMAC-SHA256 shared key), Backend proxy pattern (Main App -> Backend -> Microservice)
- Deploy: NSSM Windows Services on production server, FTPES deploy

## INVEKTOSERVICES-SPECIFIC RULES (NON-NEGOTIABLE)
These are hard codebase conventions. Violations = automatic FAIL:

### Database & SQL
- Database columns MUST be snake_case (not PascalCase, not camelCase)
- PostgreSQL 16 ONLY (parameterized queries via NpgsqlCommand/NpgsqlParameter)
- Schema source of truth: arch/db/*.sql per service
- Parameterized queries ONLY - no string concatenation in SQL
- Every new table needs GRANT ALL statement
- FK constraints must reference correct PK column names (verify from schema)

### Microservice Isolation (CRITICAL)
- Each microservice is independently deployable
- Inter-service communication ONLY via API or events (never direct DB access)
- Changes to one service MUST NOT break other services
- Shared code changes (Invekto.Shared) must be safe for ALL services
- Service-specific files stay within their service directory
- NOTE: Intentional code duplication across services is an ARCHITECTURAL DECISION, not a DRY violation

### Tenant Isolation (CRITICAL)
- Every DB query MUST filter by tenant_id
- Cross-tenant data leak = automatic FAIL
- JWT auth middleware must be active on all routes
- tenant_id from JWT claim must match request context

### Code Quality
- Error messages must be specific and actionable (not generic "An error occurred")
- Use error codes from arch/errors.md (INV-XX-NNN format: INV-AT, INV-AA, INV-OB, INV-KN, INV-AP, INV-WA)
- Enterprise-grade: handle concurrent access, avoid memory leaks, degrade gracefully under load
- System serves thousands of concurrent users under stress
- No new TODO/HACK/FIXME markers without justification
- Typed catch blocks ONLY (no bare catch(Exception))
- No null-forgiving operator (!.) - use ?. and ?? instead
- IDisposable = using/await using block, no exceptions

### Shared Component Rule
- Invekto.Shared changes affect ALL microservices
- Backend proxy changes must be verified against target service API
- Dashboard UI changes must consider all service health cards

## CQ9-12 INVEKTOSERVICES-SPECIFIC GUIDANCE

### CQ9: Business Logic Consistency
- Microservice isolation: no direct DB access between services, API/event only
- Invekto.Shared DTOs must be the single communication contract
- Tenant scoping (tenant_id filter) must be present in ALL data queries
- Backend proxy changes must match target service's actual API contract

### CQ10: UX Consistency
- Dashboard components must follow established service health card patterns
- API response format must be consistent across all services (standard envelope)
- Error responses must follow the standard error envelope format
- FlowBuilder UI changes must not break existing flow configurations

### CQ11: DB-Code Sync
- Every table/column referenced in C# code must exist in arch/db/*.sql schema files
- Entity properties must match DB column names (snake_case via NpgsqlSnakeCaseNameTranslator)
- New DB changes require: 1) arch/db/{service}.sql update, 2) GRANT ALL statement
- FK constraints must reference correct PK column names (verify from schema)

### CQ12: Error Handling Quality
- All errors must use INV-XX-NNN codes from arch/errors.md
- Error messages must include: what failed, why, and what to do next
- HTTP status codes: 400 (bad input), 401 (no auth), 403 (no permission), 404 (not found), 500 (server error)
- Typed catch blocks ONLY (no bare catch(Exception)), must log AND return meaningful response

## META-CHECK: REASONING TRACE CONSISTENCY

### CQ-META: Agent reasoning trace ile final output tutarli mi?
- Reasoning/analiz bolumunde tespit edilen bulgu, output/karar ile celisiyor mu?
- Ornek FAIL: Reasoning "tenant filter eksik" diyor ama verdict PASS veriyor
- Ornek FAIL: Analiz "silent failure riski var" diyor ama CQ2 PASS olarak isaretlenmis
- Ornek FAIL: Reasoning "microservice isolation ihlali" diyor ama PASS verdict
- Kaynak: Mount Sinai ChatGPT Health Study — CoT faithfulness yapisal LLM problemi
- Bu kural TUM CQ sonuclarini kapsar: herhangi bir CQ'nun reasoning'i ile verdict'i celisiyorsa = FAIL

## FAIL CONDITIONS (ANY = automatic FAIL)
- Tenant/auth/security regression risk
- Architecture/policy violation
- DB injection / unsafe query (string concatenation in SQL)
- Missing tenant_id filtering in DB queries
- Microservice isolation violation (one service directly accessing another's DB)
- Schema drift without migration (code uses column not in arch/db/*.sql)
- snake_case violation in DB columns
- Bare catch(Exception) without typed catch
- Null-forgiving operator (!.) usage
- N+1 query pattern
