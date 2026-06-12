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
- Every **request-path** DB query MUST filter by tenant_id
- Cross-tenant data leak = automatic FAIL
- JWT auth middleware must be active on all routes
- tenant_id from JWT claim must match request context
- **SANCTIONED EXCEPTION — background/hosted sweep jobs:** an `IHostedService`/timer worker that has NO per-request tenant context and never returns data to a caller (it iterates all tenants to maintain internal/partner state) is EXEMPT from the per-request tenant_id filter. These read all-tenant rows to act on each tenant's own data; there is no cross-tenant leak surface. Precedents in prod: `OutboundRepository.FetchPendingOutboxBatchAsync` (InmaOptOutSyncJob drains every tenant's outbox), `ResetSendingMessagesAsync`, `SweepStrandedPostingAsync`, `GetWapCrmConfiguredTenantsAsync` (CxapiWebhookReconcileJob). Do NOT FAIL these for a missing tenant_id predicate — that is the design.

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
- Tenant scoping (tenant_id filter) must be present in ALL **request-path** data queries — but background/hosted sweep jobs are the SANCTIONED EXCEPTION (see Tenant Isolation above): they legitimately read all-tenant rows to act per-tenant, with no caller-facing leak surface
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

## FEATURE-SPECIFIC INTENT (anti-false-positive guidance for Codex)

### FEAT-TFM MVP — Tenant Field Mapping (resolver + config CRUD + DI swap)
- **DbTenantFieldMappingResolver null return = INTENTIONAL contract.** Mapping yoksa veya placeholder mapping'de yoksa resolver `null` döner; çağıran `DynamicMessageValidator` raw INMA-key allowlist fallback'ine düşer. Bu bir "silent failure" DEĞİL — DMP backward-compat sözleşmesi (NullResolver davranışıyla aynı). CQ2 değerlendirirken bu davranışı silent failure olarak işaretleme.
- **NullTenantFieldMappingResolver dosyası SİLİNMEDİ.** DI binding swap edildi (3 servis), dosya korundu — test fixture + ileride per-tenant TFM disable senaryosu için intentional kalıt. CQ8 dead code tespiti yapma.
- **Multi-instance cache eventual consistency.** Backend PUT cache invalidate sadece local instance'da çalışır; Outbound/Automation cache 5dk TTL ile yenilenir. Bu kabul edilen bir MVP davranışı (cross-instance Redis pub/sub v2'ye ertelendi). CQ11 tutarlılık değerlendirirken acceptable trade-off olarak gör.
- **Reserved name guard zorunlu.** Validator `InmaDynamicFieldKeys.Allowlist` (15 INMA-native key) ∪ leads core columns'u reserved tutar — INV-BE-097 ile reject. Sebep: tenant 'name' semantic kaydederse DMP allowlist'te de raw 'name' var → ambiguous substitution. Reserved guard contract bug'ını önler.
- **leads.custom_1..custom_10 kolonlar MVP'de boş.** Sadece forward-compat ALTER (FEAT-TFM-SYNC sonraki paket dolduracak). Resolver bu kolonları okumuyor — sadece tenant_settings.field_mapping JSONB'yi çağırıyor. CQ9 unused-column tespiti yapma.
- **ITenantFieldMappingResolver.Invalidate(int) interface expansion safety.** İki impl var: NullTenantFieldMappingResolver (no-op stub) + DbTenantFieldMappingResolver (cache.Remove + _inflight.TryRemove). Tüketici sayısı: 1 (DynamicMessageValidator yalnız ResolveToInmaKeyAsync çağırıyor; Invalidate çağıran yok dışında Backend PUT endpoint). Solution-wide grep pattern: `ITenantFieldMappingResolver|ResolveToInmaKey` 11 dosyada (3 servis Program.cs DI + 2 impl + 1 interface + 1 validator + 4 plan/lessons doc). Build PASS = breaking yok. CQ8 "all consumers safely updated" proof = bu liste.
- **No-test policy.** InvektoServices CLAUDE.md "No tests, no docs unless requested" der. FEAT-TFM MVP integration test eklemiyor — bu intentional, regression doğrulaması: (1) NullResolver davranışı aynen korundu (no-op + null-on-miss), (2) DbResolver mapping yoksa null döner (NullResolver semantics ile aynı kontrat), (3) Build PASS tüm DMP test patikalarını compile-time doğrular, (4) Q deploy-smoke aşamasında structural validation yapacak (DMP pattern). CQ8/Q4 "regression test evidence" beklentisi project policy ile uyumsuz; pattern parity yeterli.

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
