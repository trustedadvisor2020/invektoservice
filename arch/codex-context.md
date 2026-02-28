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
