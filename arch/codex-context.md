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
- **SANCTIONED EXCEPTION — optional-step degradation boundary:** a caller that wraps a NON-CRITICAL optional step (one whose failure must degrade gracefully, not fail the request) MAY use `catch (Exception)` provided it (a) re-throws `OperationCanceledException` FIRST so shutdown/cancellation is never swallowed, (b) logs with context, and (c) continues with a safe fallback. This is NOT a silent failure (it is logged) and is NOT on the core path. Precedent in prod: AgentAI `/suggest` conversation-summarization wrapper (Program.cs) — summarization is a token-optimization; any failure falls back to raw history. Do NOT FAIL this specific boundary for "bare catch(Exception)".
- **SANCTIONED EXCEPTION — degradation / resilience boundaries:** a `catch (Exception)` is allowed ONLY when it is a deliberate graceful-degradation or worker-resilience boundary AND it handles cancellation first — a `catch (OperationCanceledException)` block (which `throw;`s, or `break;`s/`return;`s out of a worker loop so the host shuts down cleanly) IMMEDIATELY preceding the broad catch — AND it logs. Two precedents in prod: (1) **optional-step degradation boundary** — an optional step that falls back on any failure (e.g. semantic search → keyword search; best-effort startup recovery), here cancellation is rethrown; (2) **per-job worker-loop resilience boundary** — an `IHostedService`/`BackgroundService` loop where a single job's failure must not kill the loop, here cancellation `break`s the loop (rethrowing would fault the host). These must carry an inline comment pointing here. AgentAI ReplyGenerator/RetrievalService and DocumentProcessingService are precedents. Do NOT FAIL these — a typed-only catch would break the intended fallback/loop resilience. Everywhere else: typed catches only.
- No null-forgiving operator (!.) - use ?. and ?? instead
- IDisposable = using/await using block, no exceptions

#### SANCTIONED broad-catch — auth-endpoint resilience boundary
- An unauthenticated public auth endpoint (e.g. `POST /api/v1/flow-builder/auth/login`) is a SANCTIONED "must always return a controlled response, never crash uncontrolled to middleware" boundary. After the specific typed catches (`JsonException` → 400, `NpgsqlException` → `DatabaseConnectionFailed` 500), a trailing `catch (Exception ex)` is RETAINED to guarantee a static, code-bearing 500 (`GeneralUnknown`, "Login failed") for any genuinely-unexpected failure (e.g. JWT generation). `ex.Message` is **log-only** (`jsonLogger.StepError`), never placed in the response body → no detail leak. Do NOT FAIL this trailing broad-catch for CQ "typed catch ONLY"; narrowing it would expose the auth path to an uncontrolled middleware 500 with a divergent message/log. Same class as the `/payment/callback` always-graceful boundary.

#### SANCTIONED broad-catch — fire-and-forget background boundary
- A fire-and-forget continuation (`_ = SomeAsync(...)`, `Task.Run(...)`, or a `System.Threading.Timer` callback — e.g. WebChat `ConversationService.FireWebhookForWidgetAsync` / `TriggerAIReplyAsync`, `PushNotificationService.NotifyNewMessageAsync`, `AutomationWebhookClient.FireWebhookAsync`, the Automation webhook-intake `Task.Run(orchestrator.ProcessMessageAsync(...))` dispatch in `Invekto.Automation/Program.cs`) has **no caller** to surface an exception to: an unobserved-task fault is swallowed by the runtime (only `TaskScheduler.UnobservedTaskException` ever sees it). After the specific typed catches (`NpgsqlException` → `DatabaseConnectionFailed`, `HttpRequestException`/`JsonException` → the relevant `INV-WC-*` code), a trailing `catch (Exception ex)` is RETAINED to guarantee the fault is **logged with full type+detail** (`ex.GetType().Name` + full `ex`, code-bearing) rather than vanishing — this is the opposite of swallowing, it makes the failure observable for ops triage. There is **no response surface** (background work), so no client leak is possible. The leading cancellation catch must `return`/exit quietly, NOT `throw` (an unobserved-task rethrow is pointless and noisy); where no caller token is wired, the only reachable cancellation is the `HttpClient` transport timeout (`TaskCanceledException`) and is logged as a timeout. Do NOT FAIL this trailing broad-catch for CQ "typed catch ONLY". Same class as the auth-login and `/payment/callback` boundaries.

#### SANCTIONED broad-catch — arbitrary-handler dispatch resilience boundary
- A dispatcher that invokes **arbitrary, pluggable handlers** whose throw surface is genuinely unbounded (e.g. `Invekto.Automation/Services/FlowEngineV2.cs` executing per-node `handler.ExecuteAsync(...)` where a node may be an AI / HTTP / API-call / JSON / message handler) is a SANCTIONED resilience boundary. A leading `catch (OperationCanceledException) { throw; }` MUST propagate cancellation; the trailing `catch (Exception ex)` then degrades ANY single-handler failure to a **coded** result (here `AutomationNodeExecutionFailed` + handoff) so one misbehaving node never crashes the whole flow engine, logged with full type+detail (`ex.GetType().Name`). Enumerating typed catches here is both incomplete (the handler set grows) and pointless (every branch maps to the identical coded handoff). Do NOT FAIL this trailing broad-catch for CQ "typed catch ONLY". Same family as the optional-step / fire-and-forget boundaries.

#### SANCTIONED broad-catch — per-item / optional-step degradation boundary
- When a broad-catch sits inside a **per-item loop or lazy `.Select()` projection** computing an OPTIONAL enrichment for each element (e.g. `Invekto.Automation/Program.cs` GET `/api/v1/flows/{tenantId}` computing a per-flow health score inside the response `.Select()`), a genuinely-unexpected failure on ONE item must NOT abort the whole batch/list. After the specific typed catches covering the realistic surface (`JsonException` / `InvalidOperationException` / `ArgumentException`), a trailing `catch (Exception ex)` is RETAINED to degrade that single item to a safe default (e.g. `healthScore = 0`) and continue, **logged with full type+detail** (`ex.GetType().Name`). The enrichment is non-essential to the primary payload, the projection runs during serialization (outside the handler's outer try, so it cannot reach an outer typed catch), and there is no per-item client-error surface beyond the degraded value. Do NOT FAIL this trailing broad-catch for CQ "typed catch ONLY" — narrowing it would let one malformed row 500 the entire list. Same family as the optional-step degradation boundaries.

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

### 2026-06-14 Refactor Audit Batch 1 — GetInt32-on-bigint `::int` cast (projection-only)
- **Bu batch SADECE SELECT projeksiyonlarını değiştirir (COUNT(*)/SUM(int) kolonlarına `::int` ekler). Hiçbir WHERE / JOIN / tenant_id predicate eklemez, silmez, taşımaz veya değiştirmez.** Dokunulan bir sorguya dair her CQ9 tenant-scoping gözlemi PRE-EXISTING, değişmemiş SQL hakkındadır — bu diff tarafından sokulmadı. CQ9'u diff delta'sına göre değerlendir, çevredeki pre-existing predicate'e göre değil.
- **Appointments `GetAvailableSlotsAsync` current_bookings subquery'si (AppointmentsRepository.cs ~673) korelasyon-güvenlidir.** `(SELECT COUNT(*) FROM appointments a WHERE a.slot_id = s.id ...)` subquery'si DIŞ `appointment_slots s WHERE s.tenant_id = @tid` satırının `s.id`'sine bağlıdır → yalnızca tenant'ın KENDİ slot'larının appointment'larını sayar. Cross-tenant yüzey yok (yabancı slot_id enjekte edilemez). `::int` cast'i "appointments'ta tenant_id predicate yok" diye FAIL ETME.
- **Knowledge `ReadIntentDistributionAsync` (TemplateExtractorService.cs ~315) analysis_id-scoped'dur (pre-existing pattern, sibling `ReadFaqClustersAsync` de aynısını kullanır).** `wa_intents WHERE analysis_id = @aid` tek bir tenant-owned analizin satırlarını okur; sahiplik upstream analysisId-resolution katmanında zorlanır (`ExtractAndCompareAsync` zaten çözülmüş analysisId alır, scope'ta tenantId yok). wa_intents'te `tenant_id` + `(tenant_id, analysis_id)` index'i VAR → defense-in-depth tenant_id filtresi mümkün; ama eklemek çağrı zinciri boyunca tenantId threading (imza değişikliği) gerektirir ve sibling FAQ sorgusunu da kapsamalı — bu yüzden **ayrı bir tenant-scoping hardening batch'ine TRACKED edildi**, minimal-diff cast fix'ine bilerek bundle EDİLMEDİ. `::int` cast'i bu pre-existing predicate için FAIL ETME.
### Backend settings/instances — error-handling typed-catch sweep (work/20260614-backend-settings-instances-typed-catch)
- **GET /api/v1/settings/instances auto-fetch = sanctioned optional-step degradation boundary.** The WapCRM warm-up inside the `if (!hasRecords)` block is best-effort: the endpoint must still return the cached instance list (`ListInstancesAsync` → `Results.Ok`) even if the warm-up fails. The four typed catches (`HttpRequestException`/`TaskCanceledException`/`JsonException`/`NpgsqlException`) intentionally **log-and-continue** (no error response) — this is NOT a CQ12 "must return meaningful response" violation; the meaningful response is the cached list served immediately after. Any UNEXPECTED exception type still propagates to TrafficLoggingMiddleware. This is the existing best-effort behavior, now narrowed from a broad `catch(Exception)` to typed.
- **TaskCanceledException → coded transport timeout (not caller-cancel).** `FetchWapCrmInstances` constructs its own `HttpClient { Timeout = 10s }` and passes **NO CancellationToken**. Therefore the only `OperationCanceledException` reachable from that call is the HttpClient timeout (a transport failure), mapped to a coded 504 on /refresh and to log-and-continue on the auto-fetch. There is no caller-cancel path in these try blocks, so catching `TaskCanceledException` does not swallow a genuine client-abort. Mirrors the existing `HttpRequestException`→`TaskCanceledException` transport pairs already in this file (e.g. 5783/5802, 8901/8908).
- **ex.Message moved to logs only on /refresh.** The prior `catch(Exception)` interpolated `ex.Message` into the 502 response body (Backend-14 leak). All four typed catches now log `ex.Message` via `jsonLog.StepWarn` and return a static Turkish user-facing message. Error code reused (`BackendInstanceFetchFailed`) — Shared/errors.md untouched; status varies (502/503/504) by failure class.
### Backend payment (QNB 3DPay) — error-handling typed-catch sweep (work/20260614-backend-payment-typed-catch)
- **`vpos.InitiatePayment` is PURE — no server-side HTTP.** It builds the bank's auto-submit HTML form that the CLIENT browser POSTs to the gateway (3DPay browser-redirect flow). So `/payment/initiate`'s main try has NO transport exception to catch — the audit's suggested `catch(HttpRequestException)` would be a dead catch. The only throwing op is the pending-row INSERT → narrowed to `catch(NpgsqlException)`. The defensive `throw new InvalidOperationException("OrderId null")` is unreachable (InitiatePayment always sets OrderId) and now propagates by design. Do NOT flag the missing HttpRequestException catch.
- **`/payment/callback` broad catches are a SANCTIONED public bank-return-URL resilience boundary — intentionally LEFT broad, not in this diff.** The bank's 3D page redirects the USER'S BROWSER (form POST) to this OkUrl/FailUrl; the handler MUST always redirect the user gracefully (to `/app/#/licenses?payment_result=...`) and never surface a 500 on the payment-return path. The three `catch(Exception)` blocks (ReadFormAsync parse, ParseCallback, best-effort DB UPDATE) are the same class as the sanctioned "webhook always-2xx" rule. `vpos.ParseCallback` is itself pure (null-safe dictionary reads, cannot throw). Narrowing these would let an unexpected type break the user's return redirect for zero benefit. Do NOT flag the callback broad catches — they are out of scope by design.
- **`/payment/history` is a pure DB read endpoint** (ops-auth) → `catch(Exception)` narrowed to `catch(NpgsqlException)`. No HTTP/JSON path.
- All three converted sites keep their existing error codes (`GeneralUnknown`, `BackendPaymentInitFailed`, `BackendPaymentHistoryFailed`) and static user messages — no `ex.Message` is returned to callers (logged only). Shared/errors.md untouched; no new usings.

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
