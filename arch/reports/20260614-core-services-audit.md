# Çekirdek Servis Refactor Audit — 2026-06-14

> **Kapsam:** Backend (24.9k LOC) · Outbound (13.2k) · Automation (17.1k) · Shared (16.3k) — ~71.5k satır / 331 dosya.
> **Yöntem:** READ-ONLY. Sıfır dosya yazımı, sıfır commit, sıfır deploy. 24 work unit → per-unit review (sonnet) → her bulgu adversarial refute (sonnet) → sentez (opus).
> **Koşu notu:** İlk koşu (Opus review) geçici sunucu-taraflı rate-limit'e takıldı (23/24 ünite düştü); review sonnet'e alınıp tekrar koşuldu → 24/24 tamam.
> **İstatistik:** 138 ham bulgu → **98 doğrulanmış** (40 refute/elendi). 163 ajan, ~6M token.
> **Kullanım:** Bu bir triage backlog'u. Her madde behavior-preserving fix önerisi + fix-risk taşır. Onayladığın maddeler `auto`+Codex döngüsünden geçer.

## 1. Yönetici Özeti

Overall health: structurally sound but with a consistent, repo-wide pattern of error-handling drift and a handful of genuine runtime-throwers that have likely not surfaced only because of low row counts / low concurrency. The rot is NOT evenly spread — it concentrates in three places: (1) Backend/Program.cs (~10k-line god-file) holds the largest cluster of broad catch(Exception) + a critical auto-provision Random.Next collision + sync-over-async; (2) the Data/repository layer across ALL FOUR services shares the same two latent bugs — COUNT(*)/SUM bigint read via GetInt32 (guaranteed InvalidCastException once a counter exceeds int range, or immediately for SUM of nullable/bigint) and null-forgiving (!) repo hard-fails; (3) Invekto.Shared has two CRITICAL correctness defects that are not style issues at all: duplicate error-code VALUES (INV-BE-090..094 and INV-BE-110 each map to two different constants) and a cache invalidation/in-flight race in DbTenantCampaignResolver. The single highest-severity true bug is LeadRepository.cs:363 (GetInt32 on COUNT(*)) and the Outbound JWT-blocked internal endpoints (outbound Program.cs:301 — internal opt-out/outbox-drain are unreachable). Most findings are behavior-preserving and low-risk to fix; the dangerous ones (Random.Next provisioning, ConsentManager allow-all-on-empty, partial-state deletion/project-run) change behavior and need care + a test. Recommend: fix the bigint-cast and error-code-collision classes first (mechanical, high-confidence, real crash/contract bugs), then the security/consent items, and explicitly leave the large stable subsystems (Logging reader, FlowValidator DFS, sanctioned sweep jobs, scheduler-host ProjectReference) untouched. This is maintenance debt, not architectural failure — do not embark on a Program.cs rewrite.

## 2. Severity Dağılımı

| Servis | Critical | High | Medium | Low | Toplam |
|---|---|---|---|---|---|
| Backend | 1 | 4 | 16 | 11 | 32 |
| Outbound | 1 | 7 | 5 | 4 | 17 |
| Automation | 0 | 2 | 16 | 9 | 27 |
| Shared | 2 | 1 | 6 | 13 | 22 |
| **TOPLAM** | **4** | **14** | **43** | **37** | **98** |

**Boyut dağılımı:** error-handling (39) · correctness (39) · security (9) · duplication (8) · simplification (2) · isolation (1)

## 3. Cross-Cutting Pattern'ler (en yüksek kaldıraç)

### 3.1 COUNT(*)/SUM read with GetInt32 — bigint/Int64 column read as Int32 throws InvalidCastException at runtime (immediate for SUM/nullable, eventual for COUNT once >int _[high]_

- **Servisler:** Backend, Outbound, Automation
- **Pattern:** COUNT(*)/SUM read with GetInt32 — bigint/Int64 column read as Int32 throws InvalidCastException at runtime (immediate for SUM/nullable, eventual for COUNT once >int.MaxValue). This is a real crash, not a style nit, and it repeats identically in every data layer.
- **Öneri:** Mechanical, behavior-preserving fix per call site: use GetInt64 (then cast/clamp) or cast in SQL (COUNT(*)::int / SUM(col)::bigint). Confirmed sites: LeadRepository.cs:363 (CRITICAL — COUNT(*)), AnalyticsRepository.cs:114-121,231-232 (SUM), OutboundRepository.cs:1603,1619 (COUNT(cv.id)), ExportRepository.cs:218-232 & 352-383 (nullable template_id read as Int32 — throws for HSM/inline jobs, the more urgent ones), AutomationRepository.cs:1281-1325. Add a tiny shared reader extension (GetInt32FromCount) in Shared to stop this recurring. Verify each via the column's SQL type before choosing GetInt64 vs ::int.

### 3.2 Broad catch(Exception) with no typed NpgsqlException branch and no INV-XXX mapping — the repo's #1 hard-fail rule, violated pervasively in request handlers and a few repos/services _[medium]_

- **Servisler:** Backend, Outbound, Automation, Shared
- **Pattern:** Broad catch(Exception) with no typed NpgsqlException branch and no INV-XXX mapping — the repo's #1 hard-fail rule, violated pervasively in request handlers and a few repos/services. Several variants swallow OperationCanceledException (masks shutdown/cancellation) or interpolate Exception.Message into the HTTP response (info leak).
- **Öneri:** Triage by call-path, do NOT mass-rewrite. Priority order: (a) DB-touching handlers — add typed catch(NpgsqlException) -> INV code BEFORE the broad catch (payment endpoints Program.cs:9517-9641; working-hours 3895-3937; the 15-site Automation Program.cs cluster). (b) Strip Exception.Message from response bodies (Backend Program.cs:3447-3448). (c) Replace bare catch{} / broad catch that eats cancellation in MessageSenderService.cs:308-319,99-106,178-181 and AutomationOrchestrator.cs:487-524 with catch(OperationCanceledException){throw;} first. Each is independently shippable and behavior-preserving except where it currently hides a real failure.

### 3.3 Null-forgiving operator (!) used as a repo hard-fail across services — suppresses the exact NullReferenceException the codebase rule wants surfaced as a guarded INV error _[medium]_

- **Servisler:** Backend, Outbound, Automation, Shared
- **Pattern:** Null-forgiving operator (!) used as a repo hard-fail across services — suppresses the exact NullReferenceException the codebase rule wants surfaced as a guarded INV error.
- **Öneri:** Replace each ! with an explicit null guard or pattern-bind that maps to an INV code. Confirmed: LeadIntakeService.cs:203-205 (WelcomeFlowSlug), MessageLogRepository.cs:307, OutboundRepository.cs:353, JsonLinesLogger.cs:254 (post-dispose _writer — keep the null-check, it's masking a real lifecycle bug), AutomationOrchestrator.cs:1117,1152, Automation Program.cs:850,865,891 (flowConfig), and the four NodeHandlers (AiFaqHandler 208,325; AiIntentHandler 506). All behavior-preserving except JsonLinesLogger which exposes a latent dispose-ordering bug worth keeping visible.

### 3.4 Non-atomic multi-statement DB mutations without a transaction — fetch+update or expire+insert on separate statements/connections, creating TOCTOU / orphan-row / partial-state windows _[medium]_

- **Servisler:** Backend, Outbound, Automation
- **Pattern:** Non-atomic multi-statement DB mutations without a transaction — fetch+update or expire+insert on separate statements/connections, creating TOCTOU / orphan-row / partial-state windows.
- **Öneri:** Wrap each in a single transaction (or single round-trip CTE). Sites: LeadRepository.UpdatePipelineStatusAsync:209-255 (orphan activity row), AutomationRepository.RollbackFlowVersionAsync:511-529 (two connections — TOCTOU) and CreateSessionAsync:760-785, Outbound ProjectsService.cs:428-432 & 374-418 (project stuck 'running'/auto-complete-with-0-sends), Outbound deletion partial-state Program.cs:1908-1946 (row stuck 'pending' forever after data deleted). These are behavior-changing — gate behind a quick manual run-through per service; not all are equally urgent (deletion-stuck and project-stuck are customer-visible).

### 3.5 Per-request secret handling not using TryAddWithoutValidation on a per-request HttpRequestMessage — either DefaultRequestHeaders (cross-tenant leak risk) or Headers _[medium]_

- **Servisler:** Backend, Automation, Shared
- **Pattern:** Per-request secret handling not using TryAddWithoutValidation on a per-request HttpRequestMessage — either DefaultRequestHeaders (cross-tenant leak risk) or Headers.Add (FormatException on non-ASCII secret).
- **Öneri:** Standardize on request.Headers.TryAddWithoutValidation on a fresh HttpRequestMessage. Fix HttpInmaDynamicFieldsClient.cs:53 (Headers.Add -> TryAddWithoutValidation, throws today on non-ASCII secret chars), Backend Program.cs:9051-9052 (X-CIB-SecretKey on DefaultRequestHeaders -> per-request), TranslationHopClient.cs:58. NOTE the more serious sibling: TranslationService.cs:360 puts the Google API key in the URL query string (logged everywhere) — move to a header. All low-risk, behavior-preserving.

### 3.6 Duplicated logic/DTOs that should live once in Shared (or be deduped within Shared) — verbatim copies that will drift _[low]_

- **Servisler:** Backend, Shared
- **Pattern:** Duplicated logic/DTOs that should live once in Shared (or be deduped within Shared) — verbatim copies that will drift.
- **Öneri:** Highest-payoff: WapCrmFeatureGroupCatalogCache.cs:25-95 is a 90-line near-verbatim copy of InmaDynamicFieldsCache (extract a generic base/<T> cache). The four WapCrm*Client classes duplicate IsRedirectRateLimit/IsEnvelopeRateLimit/ParseRetryAfter/ContainsControlChars — pull into one internal helper. Lower priority: the 5x date-parse try/catch in Backend Program.cs:6985-7417 (extract one TryParseDateRange helper), WapCrmApiEnvelope duplicate of WapCrmApiResponse<T> (WapCrmInstance.cs:23-29). Do these only when you're already in the file.

## 4. Quick Wins (önce bunlar — düşük risk, behavior-preserving)

- **Fix COUNT(*) GetInt32 crash in lead funnel stats (CRITICAL, trivial)** — `src/Invekto.Backend/Data/LeadRepository.cs:363`  
  Verified by read: SELECT COUNT(*) returns bigint, read via reader.GetInt32(1) -> InvalidCastException. Funnel stats endpoint throws today for any tenant with leads. One-line fix: SQL COUNT(*)::int or GetInt64+cast. Behavior-preserving, trivial risk.
- **Resolve duplicate error-code VALUES INV-BE-090..094 and INV-BE-110** — `src/Invekto.Shared/Constants/ErrorCodes.cs:67-71 / 689-694 and :86 / :701`  
  Verified by read: BackendTranslationWarmup* (67-71) and BackendTranslation* (689-693) both claim INV-BE-090..094; FieldMappingDbUnavailable:701 and LeadIntakeInternalAuthInvalid:86 both claim INV-BE-110. Two distinct error conditions report the SAME code — breaks log triage and any code-based dispatch. Reassign one side to a free range + update errors.md. Behavior of code unchanged, only the string constant.
- **Move Google AI key out of URL query string into a header** — `src/Invekto.Backend/Services/TranslationService.cs:360`  
  API key in query string is captured by HTTP access logs, proxies and traces. Move to Authorization/x-goog-api-key header. Behavior-preserving, low risk, removes a standing secret-leak vector.
- **ExportRepository nullable template_id read as Int32 (HSM/inline jobs throw)** — `src/Invekto.Outbound/Data/ExportRepository.cs:218-232 and 352-383`  
  template_id is nullable; GetInt32 throws InvalidCastException for HSM/inline jobs (a common path), not an edge case. Use IsDBNull guard + GetInt32 or read as nullable. Behavior-preserving for non-null rows, fixes the crash for null rows.
- **Strip Exception.Message from the 502 response body** — `src/Invekto.Backend/Program.cs:3447-3448`  
  Raw upstream exception text returned to the API caller leaks internal detail. Replace with a fixed message + INV code; log the detail server-side. Trivial, behavior-preserving for clients (only the body text changes).
- **Sanitize fileName before path use in log-context reader** — `src/Invekto.Shared/Logging/Reader/LogReader.cs:263-314`  
  fileName comes from an HTTP query parameter and is used to build a path -> directory traversal. Add Path.GetFileName / whitelist before opening. Small, contained, removes a real traversal vector on an ops endpoint.

## 5. Yüksek-Değerli Refactor'lar (yapısal kazanım)

- **Extract a shared, typed DB-result reader (GetInt64/Count + nullable helpers) and sweep the GetInt32-on-bigint sites**
  - Kapsam: New small helper in Invekto.Shared (e.g. NpgsqlReaderExtensions) used by LeadRepository, AnalyticsRepository, OutboundRepository, ExportRepository, AutomationRepository at the ~7 confirmed COUNT/SUM/nullable sites.
  - Kazanım: Eliminates an entire recurring crash class in one motion and prevents the next repo from reintroducing it. Each call-site change is mechanical and individually verifiable.
  - Risk: Low and behavior-preserving where the column genuinely fits in int; the only judgement is COUNT(*)::int (clamp acceptable) vs GetInt64 (exact). Risk is per-site, not systemic. Recommend doing it incrementally, not as one big PR, so each service builds/tests independently.
- **De-duplicate the WapCrm rate-limit/redirect/envelope helpers and the two near-identical catalog caches in Shared**
  - Kapsam: WapCrmFeatureGroupCatalogCache vs InmaDynamicFieldsCache (~90 dup lines) -> generic base; IsRedirectRateLimit/IsEnvelopeRateLimit/ParseRetryAfter/ContainsControlChars across the 4 WapCrm*Client classes -> one internal helper; AllowAutoRedirect=false on the inma_dynamicfields HttpClient registration (Backend Program.cs:487-500) folded into the same hardening pass.
  - Kazanım: Removes drift risk on the WapCRM/cxapi integration surface — the most actively changed area (rate-limit handling already bit the Medipol run). Single source for redirect/429 semantics.
  - Risk: Medium: this touches the live outbound integration path. The cache merge is the riskier piece (cache key/TTL semantics differ — the catalog cache TTL was just changed to 1h). Do behind a build + a manual cxapi smoke test; keep the public method shapes identical.
- **Carve the error-handling debt out of Backend/Program.cs by endpoint group, NOT a rewrite**
  - Kapsam: Move the payment, settings/instances, flow-builder/working-hours, and analytics endpoint groups into typed catch(NpgsqlException)->INV + extracted helpers (TryParseDateRange, FetchWapCrmInstances callers). Optionally relocate each group into its own *Endpoints.cs (the pattern already exists, e.g. MetaLeadgenEndpoints.cs).
  - Kazanım: Shrinks the 10k-line god-file along natural seams, fixes the densest broad-catch cluster, and makes the file reviewable. The MetaLeadgenEndpoints split is the proven template.
  - Risk: Medium and easy to get wrong if done all at once. Do ONE endpoint group per PR, build + hit the routes after each. Pure relocation is behavior-preserving; the catch-typing changes behavior on the error path only (untyped 500 -> typed INV) which is the intended improvement. Do not attempt a full Program.cs restructure.

## 6. DOKUNMA (stabil — refactor risk-karşılığı-getirisiz)

- Background/hosted SWEEP jobs that query across all tenants without a tenant_id filter (FetchPendingOutboxBatchAsync / ResetSendingMessagesAsync precedent) — sanctioned, not a violation. Leave as-is.
- Backend.csproj ProjectReference with PrivateAssets=all to other services — intentional scheduler-host reflection pattern; not a microservice-isolation breach.
- Invekto.Shared/Logging LogReader DFS/filter internals beyond the two pin-pointed fixes (path-traversal sanitize + the nullable-boolean filter inversion at 123-131). The bare-catch blocks there are low-value to churn; fix only if you're already editing the reader.
- FlowValidator DFS cycle detection (FlowValidator.cs:359-381) — the omitted intermediate-node warning is cosmetic validator output, not a runtime bug; rewriting graph traversal risks regressions for zero user-visible payoff.
- SimulationEngine concurrency note (146-219) — simulation is a single-user design tool, not a concurrent production path; adding locks is over-engineering. Leave unless simulation becomes multi-user.
- UpsertLeadPreferredLocaleAsync dead PostgresException(23505) catch (AutomationRepository.cs:1001-1012) and the locale null-guard dead code in DbTenantCampaignResolver (170,177) — harmless dead code; not worth a touch in stable paths.
- Stable, working integration code that you are not otherwise modifying — per repo rule, do not refactor for its own sake. Apply the fixes above opportunistically when already in a file, not as a standalone refactor campaign across these areas.
- The unauthenticated external webhook endpoint (Automation Program.cs:1770-1773) IF it is the documented INMA-callback contract — confirm against arch/contracts before changing; adding auth there could break the live INMA->INSE bridge. Flag for Q decision, do not unilaterally add auth.

## 7. Doğrulanmış Bulgular (servis → severity sırası)

### Backend (32)

#### Backend-1 · [CRITICAL] COUNT(*) bigint read via GetInt32 — InvalidCastException at runtime

- **Dosya:** `src/Invekto.Backend/Data/LeadRepository.cs` (347-365)
- **Boyut:** correctness · **Fix-risk:** trivial · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** GetFunnelStatsAsync executes `SELECT pipeline_status, COUNT(*) as cnt FROM leads GROUP BY pipeline_status` and reads the count column with `reader.GetInt32(1)`. PostgreSQL COUNT(*) returns bigint (int8), not int4. Npgsql enforces strict type mapping: GetInt32 on a bigint column throws InvalidCastException. This blows up on every call to the funnel-stats endpoint for any tenant that has at least one lead in the leads table.
- **Kanıt:** `Line 348: `SELECT pipeline_status, COUNT(*) as cnt` — line 363: `var count = reader.GetInt32(1);``
- **Önerilen fix:** Either cast in SQL: `COUNT(*)::int` so the wire type is int4 and GetInt32 is safe; or change the reader call to `reader.GetInt64(1)` and widen the local variable and `ByStatus` dictionary value type to long/int64. The SQL cast is lower-risk and keeps the DTO shape unchanged.

#### Backend-2 · [HIGH] Broad catch(Exception) in FlowBuilder auth/login endpoint

- **Dosya:** `src/Invekto.Backend/Program.cs` (4871-4877)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** The /api/v1/flow-builder/auth/login handler wraps the entire DB read + JWT generation block in a catch(Exception ex) that maps everything to a generic 500. The inner block performs a raw NpgsqlCommand query without any NpgsqlException catch; any DB error (connection dropped, constraint violation, timeout) falls to the broad catch instead of being identified as a DB error. This is a repo hard-fail pattern: broad catch(Exception) is forbidden even when it maps to a coded response.
- **Kanıt:** `catch (Exception ex) { jsonLogger.StepError($"FlowBuilder login error: {ex.Message}", requestId); return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Login failed", requestId), statusCode: 500); }`
- **Önerilen fix:** Add a typed catch (NpgsqlException ex) before the broad catch to map DB errors to a distinct INV error code (e.g. ErrorCodes.DatabaseConnectionFailed with 503). Then replace the broad catch(Exception) with catch(InvalidOperationException) or remove it entirely — only the JsonException path (line 4865) and the NpgsqlException path need explicit catches; any remaining unhandled exception should propagate to ASP.NET's global error handler.

#### Backend-3 · [HIGH] Broad catch(Exception) in ResolveTranslateTenantAsync silently falls through to auto-provision on transient DB errors

- **Dosya:** `src/Invekto.Backend/Program.cs` (9675-9678)
- **Boyut:** error-handling · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** In ResolveTranslateTenantAsync, the inma_code lookup at line 9667 is wrapped in `catch (Exception ex)` at line 9675 which logs and falls through. When this catch fires due to a transient DB error (e.g., connection reset), execution falls through to step 3 (auto-provision), creating a spurious new tenant row for a code that already has a real tenant. The next request may then resolve to the auto-provisioned row instead of the real tenant. The correct fix is to catch NpgsqlException only and re-throw (or return error) on unexpected exception types.
- **Kanıt:** `catch (Exception ex) { log.StepError($"[translate-resolve] PG inma_code lookup error: {ex.Message}", "-"); } // falls through to auto-provision`
- **Önerilen fix:** Change to `catch (Npgsql.NpgsqlException ex)` and return an error tuple `(0, "DB hatasi")` instead of falling through to auto-provision. Auto-provision should only run when the lookup definitively returns no rows, not when the query itself failed.

#### Backend-4 · [HIGH] Google AI Studio API key embedded in URL query string — exposed in HTTP logs and traces

- **Dosya:** `src/Invekto.Backend/Services/TranslationService.cs` (360)
- **Boyut:** security · **Fix-risk:** low · behavior-preserving
- **Sorun:** The Google AI Studio API key is placed in the URL as a query parameter: `?key={_googleApiKey}`. Any HTTP access log (IIS, nginx, NSSM stdout), ASP.NET Core HttpClient diagnostic event source, or outbound proxy will capture the full URL including the secret key. Google AI Studio also supports the `x-goog-api-key` request header for authentication, which keeps the secret out of URLs and logs.
- **Kanıt:** `var url = $"{GoogleAiStudioUrl}/{_googleModel}:generateContent?key={_googleApiKey}";`
- **Önerilen fix:** Build the URL without the key: `var url = $"{GoogleAiStudioUrl}/{_googleModel}:generateContent";` and add the key as a request header: `httpRequest.Headers.TryAddWithoutValidation("x-goog-api-key", _googleApiKey);` — Google AI Studio accepts this header as equivalent to the query parameter.

#### Backend-5 · [HIGH] GetInt32 on SUM(INTEGER) columns throws InvalidCastException at runtime

- **Dosya:** `src/Invekto.Backend/Services/AnalyticsRepository.cs` (114-121, 231-232)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving
- **Sorun:** PostgreSQL SUM() of an INTEGER column returns bigint (int8), not int4. Two methods call GetInt32 on SUM results: GetAutomationSummaryAsync reads SUM(total_replies), SUM(deflected_count), SUM(handoff_count), SUM(faq_count), SUM(intent_count), SUM(menu_count), SUM(off_hours_count), SUM(welcome_count) at lines 114-121 — all via GetInt32. GetIntentMetricsAsync reads SUM(total_count) at line 231 and SUM(handoff_count) at line 232 via GetInt32. Every non-empty date range query on these paths throws InvalidCastException, making the automation analytics dashboard completely broken for any tenant with data in daily_metrics or daily_intent_metrics. Schema confirmed: daily_metrics.total_replies etc. are INTEGER (arch/db/backend-metrics.sql lines 16-29), daily_intent_metrics.total_count and handoff_count are INTEGER (lines 48-49).
- **Kanıt:** `Line 114: `summary.TotalReplies = reader.GetInt32(0);` reading ordinal 0 = `COALESCE(SUM(total_replies), 0)`. Line 231: `var total = reader.GetInt32(1);` reading ordinal 1 = `SUM(total_count) AS total`.`
- **Önerilen fix:** Option A (SQL cast — lowest risk): Append `::int` to each SUM in the SELECT list: `COALESCE(SUM(total_replies), 0)::int`, `COALESCE(SUM(deflected_count), 0)::int`, etc. at lines 85-102, and `SUM(total_count)::int`, `SUM(handoff_count)::int` at lines 209-210. Option B (C# side): Replace `reader.GetInt32(n)` with `(int)reader.GetInt64(n)` — matches the pattern already used correctly in GetWaAgentMetricsAsync (lines 384-387) and GetWaTrendsAsync (lines 434-437).

#### Backend-6 · [MEDIUM] Broad catch(Exception) in FlowBuilder login endpoint swallows DB and unexpected errors without error code mapping

- **Dosya:** `src/Invekto.Backend/Program.cs` (4871-4877)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** The /api/v1/flow-builder/auth/login endpoint catches `JsonException` specifically (line 4865), then falls through to a bare `catch (Exception ex)` at line 4871 that maps every other throwable (NpgsqlException, OperationCanceledException, InvalidOperationException, network errors) to the same generic ErrorCodes.GeneralUnknown / 500 response. Per the repo hard-fail rule, broad catch(Exception) is forbidden even without a when() filter — each error type must be caught by its typed exception and mapped to an INV-XXX error code. A NpgsqlException from the tenant settings SELECT gets no INV code; a DB timeout is indistinguishable from a programmer bug to the caller and to the log.
- **Kanıt:** `catch (Exception ex) ⏎ { ⏎     jsonLogger.StepError($"FlowBuilder login error: {ex.Message}", requestId); ⏎     return Results.Json( ⏎         ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Login failed", requestId), ⏎         statusCode: 500); ⏎ }`
- **Önerilen fix:** Replace the broad catch with typed handlers: `catch (NpgsqlException ex) { /* INV-DB-XXX, 503 */ }` then `catch (OperationCanceledException) { /* 499/408 */ }`. Any remaining unexpected type can then optionally be allowed to propagate (unhandled 500) or wrapped in `catch (Exception ex) when (false) { }` — but every anticipated path needs a typed catch with its own INV code per arch/errors.md.

#### Backend-7 · [MEDIUM] Broad catch(Exception) in payment endpoints — NpgsqlException not typed; DB errors get no INV code distinction

- **Dosya:** `src/Invekto.Backend/Program.cs` (9517-9521, 9573-9577, 9637-9641)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** Three payment-related blocks use bare `catch (Exception ex)`: (1) line 9517 in POST /api/v1/payment/initiate — covers both the QnbVPosService call and the Npgsql INSERT, meaning a DB error and a card-gateway error are logged identically as BackendPaymentInitFailed. (2) line 9573 in the payment callback DB UPDATE — silent catch is acceptable here (redirect still happens) but NpgsqlException is not distinguished from a programmer bug. (3) line 9637 in GET /api/v1/payment/history — NpgsqlException should be mapped to a distinct INV code. The hard-fail rule requires typed catches. Additionally lines 9529 and 9538 use `catch (Exception ex)` to catch form-parse and callback-parse errors — those are broad catches over infrastructure (System.IO, framework) calls that could mask genuine bugs.
- **Kanıt:** `catch (Exception ex) ⏎ { ⏎     jsonLog.SystemWarn($"Payment initiate failed ({ErrorCodes.BackendPaymentInitFailed}): tenant={tenantId}, {ex.Message}"); ⏎     return Results.Json(new { error = ErrorCodes.BackendPaymentInitFailed, message = "Ödeme başlatılamadı." }, statusCode: 500); ⏎ }`
- **Önerilen fix:** In each payment handler, add `catch (NpgsqlException ex)` before the broad catch (mapping to INV-DB-XXX or a payment-specific code), `catch (HttpRequestException ex)` if the VPos makes HTTP calls, and let remaining unexpected types propagate rather than swallowing them.

#### Backend-8 · [MEDIUM] Inline SQL in /api/v1/flow-builder/tenant/working-hours uses raw pgFactory without NpgsqlException catch

- **Dosya:** `src/Invekto.Backend/Program.cs` (3895-3937)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** The GET /api/v1/flow-builder/tenant/working-hours endpoint at line 3895 opens a Postgres connection and executes a command without any NpgsqlException handler. A DB connection failure or query error will propagate as an unhandled exception and produce a generic 500 from the framework (with a stack trace in development mode). All other tenant endpoints that do inline DB work (e.g. /api/v1/settings/working-hours at line 3642) do correctly catch NpgsqlException. The bare `catch (Exception ex)` at line 3933 only covers the JSON parse of the returned value, not the DB call.
- **Kanıt:** `await using var conn = await pgFactory.OpenConnectionAsync(); ⏎ await using var cmd = conn.CreateCommand(); ⏎ cmd.CommandText = "SELECT settings_json->..." ⏎ // no NpgsqlException catch around the DB open/execute`
- **Önerilen fix:** Wrap the entire DB open+execute block in `try { ... } catch (NpgsqlException ex) { jsonLog.StepWarn(...); return Results.Ok(new { configured = false }); }` (consistent with the existing JSON-parse catch).

#### Backend-9 · [MEDIUM] Translation tenant auto-provision uses Random.Next — collision risk on concurrent new-tenant requests

- **Dosya:** `src/Invekto.Backend/Program.cs` (9681-9694)
- **Boyut:** correctness · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** At line 9681, `new Random().Next(10000000, 99999999)` generates the new tenant_id for auto-provisioning an inma_code. The ON CONFLICT(tenant_id) DO UPDATE SET inma_code at line 9686 means a collision will silently update a DIFFERENT tenant's inma_code to the new code, corrupting data. While the 90-million-value range makes collision unlikely per invocation, under concurrent load (multiple Automation services calling /api/v1/translate for new tenants simultaneously) the probability is not negligible. Additionally `new Random()` without a seed uses time-based seeding, which can produce identical values across threads started in the same millisecond.
- **Kanıt:** `var newTenantId = new Random().Next(10000000, 99999999); ⏎ ... ⏎ ON CONFLICT (tenant_id) DO UPDATE SET inma_code = @code`
- **Önerilen fix:** Use a SEQUENCE or a PostgreSQL `nextval('tenant_id_seq')` for the new tenant_id. Alternatively, change the ON CONFLICT to DO NOTHING and RETURNING id, then detect the null return (indicating collision) and retry. Using `Random.Shared.Next()` is safer than `new Random()` for thread safety but does not solve the collision-overwrites-wrong-tenant issue.

#### Backend-10 · [MEDIUM] Broad catch(Exception) in /api/v1/settings/instances/refresh — swallows typed errors without INV code

- **Dosya:** `src/Invekto.Backend/Program.cs` (3436-3449)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** The POST /api/v1/settings/instances/refresh endpoint wraps FetchWapCrmInstances + UpsertInstancesAsync in a single catch(Exception ex). This violates the repo hard-fail rule (broad catch(Exception) is forbidden). It conflates transport failures from HttpClient (HttpRequestException), DB failures (NpgsqlException), and unexpected logic errors into a single 502 with no INV error-code distinction. A DB error on UpsertInstancesAsync particularly should not be surfaced as a '502 WapCRM connection error'.
- **Kanıt:** `catch (Exception ex) { jsonLog.StepWarn($"WapCRM instance refresh failed: {ex.Message}", requestId); return Results.Json(new { error = ErrorCodes.BackendInstanceFetchFailed, message = $"WapCRM baglanti hatasi: {ex.Message}" }, statusCode: 502); }`
- **Önerilen fix:** Split into typed catches: catch(HttpRequestException) -> 502 WapCRM transport error; catch(NpgsqlException) -> 503 DB error with a distinct INV code (e.g. ErrorCodes.BackendInstanceFetchFailed still but status 503). Also note ex.Message is interpolated directly into the 502 response body — this can leak internal detail; log the message and return a static user-facing string instead.

#### Backend-11 · [MEDIUM] Broad catch(Exception) in GET /api/v1/flow-builder/tenant/working-hours — JSON parse swallowed silently

- **Dosya:** `src/Invekto.Backend/Program.cs` (3933-3937)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** The working-hours tooltip endpoint for the FlowBuilder uses catch(Exception ex) to swallow JSON parse failures and returns {configured:false} as if no hours were configured. This is again a repo hard-fail pattern. It is arguably less dangerous here because parse failure is treated as 'no data', but it also masks any bug in the JSON schema. Additionally, there is no NpgsqlException catch on the raw SQL at lines 3895-3899 — if the DB connection drops, the exception propagates unhandled.
- **Kanıt:** `catch (Exception ex) { jsonLog.StepWarn($"Working hours JSON parse failed for tenant {tenant.TenantId}: {ex.Message}", "-"); return Results.Ok(new { configured = false }); }`
- **Önerilen fix:** Replace catch(Exception) with catch(JsonException). Add a separate catch(NpgsqlException ex) around the raw SQL execution (lines 3895-3899) that maps to a 500 with a coded INV error response, matching the pattern used in the sibling GET /api/v1/settings/working-hours at line 3642.

#### Backend-12 · [MEDIUM] Broad catch(Exception) in GET /api/v1/settings/instances — auto-fetch silently swallows all errors including DB errors

- **Dosya:** `src/Invekto.Backend/Program.cs` (3406-3409)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** During the auto-fetch path inside GET /api/v1/settings/instances, a broad catch(Exception) swallows any error from FetchWapCrmInstances or UpsertInstancesAsync with only a warning log. This is an intentional 'best-effort' pattern but is forbidden by repo rules. A DB write failure on UpsertInstancesAsync (NpgsqlException) is silently eaten here, which means the tenant's instance list is stale without any alerting to the caller.
- **Kanıt:** `catch (Exception ex) { jsonLog.StepWarn($"Instance auto-fetch from WapCRM failed: {ex.Message}", requestId); }`
- **Önerilen fix:** Split into typed catches: catch(HttpRequestException) for WapCRM transport errors; catch(NpgsqlException) for DB errors (log at SystemWarn to differentiate from vendor failures). This is behavior-preserving since both continue to the ListInstancesAsync call below.

#### Backend-13 · [MEDIUM] Broad catch(Exception) in PUT /api/v1/settings/instances/{id}/toggle — JSON body read silently swallows non-JSON errors

- **Dosya:** `src/Invekto.Backend/Program.cs` (3566-3573)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The body-reading try/catch for the instance toggle endpoint uses catch(Exception) instead of catch(JsonException). An OperationCanceledException (client disconnect) or IOException (body read failure) would be swallowed and mapped to a 400 Bad Request with 'Invalid JSON body', which is a misleading response for those cases.
- **Kanıt:** `catch (Exception) { return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "Invalid JSON body" }); }`
- **Önerilen fix:** Change to catch(JsonException). An OperationCanceledException should propagate; IOException can be caught separately with a 400 if desired. Identical pattern fix was correctly applied to the sibling PUT /api/v1/settings/working-hours at line 3667 (catch(JsonException)) — make this endpoint consistent.

#### Backend-14 · [MEDIUM] Exception.Message interpolated directly into 502 response body in /api/v1/settings/instances/refresh

- **Dosya:** `src/Invekto.Backend/Program.cs` (3447-3448)
- **Boyut:** security · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The catch(Exception) block interpolates ex.Message into the message field of the HTTP 502 response that is sent to the SPA: `message = $"WapCRM baglanti hatasi: {ex.Message}"`. Exception messages can contain internal paths, connection strings, stack frame snippets, or other implementation details. This is an information-disclosure risk in production.
- **Kanıt:** `return Results.Json(new { error = ErrorCodes.BackendInstanceFetchFailed, message = $"WapCRM baglanti hatasi: {ex.Message}" }, statusCode: 502);`
- **Önerilen fix:** Log ex.Message to jsonLog (already done on the line above) and return a static user-facing string: `message = "WapCRM bağlantı hatası; lütfen daha sonra tekrar deneyin."`

#### Backend-15 · [MEDIUM] Identical date-parse try/catch block duplicated verbatim across 5 analytics/attribution endpoints

- **Dosya:** `src/Invekto.Backend/Program.cs` (6985-6999, 7026-7040, 7067-7081, 7364-7384, 7396-7417)
- **Boyut:** duplication · **Fix-risk:** low · behavior-preserving
- **Sorun:** The pattern `DateOnly toDate, fromDate; try { toDate = ...; fromDate = ...; } catch (FormatException) { return Results.BadRequest(...); } if (fromDate > toDate) { return Results.BadRequest(...); }` is copy-pasted verbatim into 5 separate analytics/attribution endpoints. Any change to validation logic (e.g., clamping the range, different default window for attribution vs automation) must be applied to all 5 copies. This is a pure duplication finding with no current bug, but it is the kind of spread that causes silent regressions.
- **Kanıt:** `Lines 6988/7029/7070/7367/7399 all contain: `toDate = string.IsNullOrEmpty(to) ? DateOnly.FromDateTime(DateTime.UtcNow) : DateOnly.Parse(to);``
- **Önerilen fix:** Extract a local function `static bool TryParseDateRange(string? from, string? to, int defaultDays, out DateOnly fromDate, out DateOnly toDate, out IResult? error)` and call it from all 5 endpoints.

#### Backend-16 · [MEDIUM] UpdatePipelineStatusAsync — three SQL statements run without a transaction (race + orphan activity row risk)

- **Dosya:** `src/Invekto.Backend/Data/LeadRepository.cs` (209-255)
- **Boyut:** correctness · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR · _(orijinal sev: high → düzeltildi)_
- **Sorun:** The method opens one connection and fires three sequential statements: (1) SELECT pipeline_status to capture oldStatus, (2) UPDATE leads, (3) INSERT lead_activities. There is no BEGIN/COMMIT wrapping them. Two failure modes: (a) a crash or exception between steps 2 and 3 leaves the leads row with a new pipeline_status but no audit activity, silently breaking the activity trail; (b) a concurrent DELETE on the lead between steps 1 and 2 means the UPDATE affects 0 rows (silently, since ExecuteNonQueryAsync result is ignored) and the subsequent INSERT will violate the lead_activities.lead_id FK if that FK is enforced, producing an unhandled NpgsqlException that propagates raw to the caller with no INV-XXX code. Additionally, the UPDATE row-count is never checked, so a not-found lead that vanishes after the SELECT still returns true.
- **Kanıt:** `Lines 229-254: single `conn` opened, no `BeginTransactionAsync`, three separate command executions. `updateCmd.ExecuteNonQueryAsync(ct)` result discarded on line 244.`
- **Önerilen fix:** Wrap all three statements in a BEGIN/COMMIT transaction using `await conn.BeginTransactionAsync(ct)`. Assign the transaction to each NpgsqlCommand. Check `ExecuteNonQueryAsync` row count on the UPDATE and return false (with a rollback) if rows == 0 — this replaces the prior SELECT for existence check and makes the whole operation atomic. Catch NpgsqlException around the block and map to an appropriate INV-XXX error code.

#### Backend-17 · [MEDIUM] Null-forgiving operator (!) on WelcomeFlowSlug in IntakeAsync — repo hard fail

- **Dosya:** `src/Invekto.Backend/Services/LeadIntakeService.cs` (203-205)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** Line 205 uses the null-forgiving operator `tls.WelcomeFlowSlug!` after the `IsNullOrWhiteSpace` branch already proves the value is non-null. While the compiler would not generate a NullReferenceException here (the check does guard it), the `!` operator is a blanket repo hard-fail regardless of context — it suppresses nullable analysis and sets a precedent that could silently mask a future refactor where the branch condition changes. Per project rules, pattern-bind or a typed intermediate is required.
- **Kanıt:** `Line 203-205: `var slug = string.IsNullOrWhiteSpace(tls.WelcomeFlowSlug) ? "welcome_default" : tls.WelcomeFlowSlug!;``
- **Önerilen fix:** Replace with a local capture that satisfies the nullable analyzer without `!`: `var rawSlug = tls.WelcomeFlowSlug; var slug = string.IsNullOrWhiteSpace(rawSlug) ? "welcome_default" : rawSlug;` — rawSlug is inferred non-null in the else branch by flow analysis, no `!` needed.

#### Backend-18 · [MEDIUM] PickNonNull allows empty-string to overwrite stored secrets — silently disables signature verification

- **Dosya:** `src/Invekto.Backend/Services/MetaLeadgen/MetaLeadgenEndpoints.cs` (254-262, 607-608)
- **Boyut:** security · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** The null-means-keep merge logic at lines 255-262 uses PickNonNull (line 607-608: `incoming is null ? existing : incoming`). This means a PUT body with `{"app_secret": "", "verify_token": ""}` passes the `request is null` guard (line 232) and writes empty strings to the JSONB. After that write, MetaLeadgenSignatureValidator.Verify returns false on every webhook (line 41: `if (string.IsNullOrEmpty(appSecret)) return false;`) silently rejecting all Meta webhooks with 401. An operator who accidentally sends an empty field (or a frontend bug that sends empty-string for unchanged secrets) can disable the entire Meta lead ingestion pipeline without any warning. The correct intent is: `null OR whitespace means keep`.
- **Kanıt:** `PickNonNull line 607-608: `incoming is null ? existing : incoming` — `""` is not null, so it passes through. MetaLeadgenSignatureValidator line 41: `if (string.IsNullOrEmpty(appSecret)) return false;` — the consequence is silent rejection of all future webhooks.`
- **Önerilen fix:** Change PickNonNull to treat empty/whitespace the same as null: `private static string? PickNonNull(string? incoming, string? existing) => string.IsNullOrWhiteSpace(incoming) ? existing : incoming;` Also add a validation guard after the merge: if merged.AppSecret is null or empty, return a 400 with ErrorCodes.GeneralValidation rather than persisting an unusable config.

#### Backend-19 · [MEDIUM] Bare catch{} in ValidateApiUrlsAsync URL-probe lambda swallows all exceptions

- **Dosya:** `src/Invekto.Backend/Services/ClaudeWizardService.cs` (547-554)
- **Boyut:** error-handling · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR · _(orijinal sev: high → düzeltildi)_
- **Sorun:** The URL-probe lambda inside ValidateApiUrlsAsync catches OCE only when ct.IsCancellationRequested, then has a bare `catch { }` that silently converts every other exception — including OperationCanceledException from the inner 5-second CTS, NullReferenceException, ObjectDisposedException, etc. — into an ApiUrlFailure record. Per repo rules, broad catch (even without a when filter) is forbidden and must be replaced with typed catches + INV-xxx error code mapping. The inner CTS timeout (5 s) fires as OperationCanceledException with ct.IsCancellationRequested==false, so it falls into the bare catch and is silently mapped to a URL failure — which is semantically wrong (timeout != unreachable server; retry logic built on that assumption would be subtly broken).
- **Kanıt:** `catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; } catch { return new ApiUrlFailure(label, uri.Host); }`
- **Önerilen fix:** Replace the bare `catch` with typed catches: `catch (OperationCanceledException) { return new ApiUrlFailure(label, uri.Host + " (timeout)"); } catch (HttpRequestException) { return new ApiUrlFailure(label, uri.Host); }`. This separates timeout (server reachable but slow) from network error (unreachable), and stops swallowing unexpected exceptions.

#### Backend-20 · [MEDIUM] Bare catch{} in DetectLanguageCodeAsync silently swallows all Gemma failures and falls through to Claude

- **Dosya:** `src/Invekto.Backend/Services/TranslationService.cs` (300-301)
- **Boyut:** error-handling · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** The inner try/bare-catch around CallGemmaRawAsync is used as a primary→fallback switch: any exception from Gemma (including OperationCanceledException from the caller's CancellationToken, which means app shutdown) is caught bare and silently triggers CallClaudeRawAsync. This means a user-triggered cancellation will silently retry via Claude instead of propagating cancellation. Per repo rules, broad catch is forbidden; OperationCanceledException must propagate.
- **Kanıt:** `try { response = await CallGemmaRawAsync(system, text, 10, ct); } catch { response = await CallClaudeRawAsync(system, text, 10, ct); }`
- **Önerilen fix:** Replace with `catch (HttpRequestException) catch (InvalidOperationException) catch (TaskCanceledException tce) when (!ct.IsCancellationRequested)` — letting external cancellation propagate while still falling back on transient Gemma failures.

#### Backend-21 · [MEDIUM] Null-forgiving operator (!) hard-fail in GetMessageStoryAsync

- **Dosya:** `src/Invekto.Backend/Data/MessageLogRepository.cs` (307)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The null-forgiving operator `!` is a repo hard-fail pattern. `(bool)(await hasInstCmd.ExecuteScalarAsync(ct))!` suppresses nullability on the result of ExecuteScalarAsync (which returns object?). While SELECT EXISTS(...) in PostgreSQL always returns a non-null boolean, the `!` operator bypasses null-safety analysis and is forbidden by repo code-quality rules regardless of runtime probability.
- **Kanıt:** `Line 307: `var hasInstances = (bool)(await hasInstCmd.ExecuteScalarAsync(ct))!;``
- **Önerilen fix:** Replace with a pattern-bind: `var hasInstances = await hasInstCmd.ExecuteScalarAsync(ct) is true;` — this is safe (returns true only if the scalar is actually the boolean true), eliminates the null-forgiving operator, and is semantically identical given that EXISTS always returns a non-null boolean.

#### Backend-22 · [LOW] Broad catch(Exception) in /api/ops/services/{serviceName}/restart swallows non-InvalidOperationException Windows Service errors without INV code

- **Dosya:** `src/Invekto.Backend/Program.cs` (1786-1794)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** The service restart endpoint at line 1758 catches `InvalidOperationException` (service not found) correctly, then at line 1786 falls through to `catch (Exception ex)` which silently eats `UnauthorizedAccessException` (permissions), `Win32Exception` (SCM errors), and `TimeoutException` (WaitForStatus) into the same generic success=false shape with no INV error code. The rule is broad catch(Exception) is a hard fail regardless. Additionally this endpoint returns HTTP 200 even on error, which prevents callers from distinguishing errors by status code.
- **Kanıt:** `catch (Exception ex) ⏎ { ⏎     return Results.Ok(new ⏎     { ⏎         success = false, ⏎         service = serviceName, ⏎         message = $"Yeniden baslatma hatasi: {ex.Message}" ⏎     }); ⏎ }`
- **Önerilen fix:** Add `catch (System.ComponentModel.Win32Exception ex)` for SCM permission errors with an appropriate INV code, `catch (System.TimeoutException ex)` for the WaitForStatus timeout, and keep a final narrow catch or let unexpected exceptions propagate. Return HTTP 500/503 instead of 200 for error paths.

#### Backend-23 · [LOW] Broad catch(Exception) in FetchWapCrmInstances callers — HttpRequestException / TimeoutException not typed

- **Dosya:** `src/Invekto.Backend/Program.cs` (3406-3409, 3445-3449)
- **Boyut:** error-handling · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** Two callers of FetchWapCrmInstances — the auto-fetch block in GET /api/v1/settings/instances (line 3406) and the refresh endpoint (line 3445) — wrap the call in `catch (Exception ex)`. FetchWapCrmInstances itself throws `HttpRequestException` (network failure), `TaskCanceledException` (timeout from the raw new HttpClient), and `JsonException` (malformed cxapi response). These are all distinct error categories. The broad catch treats them the same way and logs only `ex.Message`. The rule forbids broad catch(Exception). Additionally, FetchWapCrmInstances at line 3943 instantiates `new HttpClient()` directly (not from IHttpClientFactory) and sets `DefaultRequestHeaders.Add("X-CIB-SecretKey", secretKey)` — this is a per-request secret placed on shared DefaultRequestHeaders which is the exact per-request-secrets anti-pattern called out in the hard-fail rules (line 3944).
- **Kanıt:** `using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) }; ⏎ httpClient.DefaultRequestHeaders.Add("X-CIB-SecretKey", secretKey);`
- **Önerilen fix:** For the catch sites: use `catch (HttpRequestException ex)` + `catch (TaskCanceledException ex)` + `catch (JsonException ex)` each with distinct INV codes. For the HttpClient: use `IHttpClientFactory` (named client already exists for similar pattern) and pass X-CIB-SecretKey via `HttpRequestMessage.Headers.TryAddWithoutValidation` per-request rather than DefaultRequestHeaders.

#### Backend-24 · [LOW] Broad catch(Exception) in /ops/debug2 log parse is an ops-only endpoint but still violates the hard-fail rule

- **Dosya:** `src/Invekto.Backend/Program.cs` (1137-1140)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The debug endpoint at line 1133 wraps `JsonSerializer.Deserialize<object>` in `catch (Exception ex)`. Only `JsonException` and `ArgumentNullException` can be thrown here; a bare catch is a hard-fail pattern. The impact is low (ops-only diagnostic endpoint), but the rule applies regardless.
- **Kanıt:** `catch (Exception ex) ⏎ { ⏎     parseError = ex.Message; ⏎ }`
- **Önerilen fix:** Replace with `catch (JsonException ex) { parseError = ex.Message; }`.

#### Backend-25 · [LOW] Broad catch(Exception) in instance toggle endpoint body parse — should be typed JsonException

- **Dosya:** `src/Invekto.Backend/Program.cs` (3569-3573)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** PUT /api/v1/settings/instances/{instanceId}/toggle at line 3568 catches `catch (Exception)` (no variable, note bare pattern) around `ReadFromJsonAsync<JsonElement>`. Only `JsonException` or `BadHttpRequestException` can be thrown here. The broad catch is a hard-fail violation.
- **Kanıt:** `catch (Exception) ⏎ { ⏎     return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "Invalid JSON body" }); ⏎ }`
- **Önerilen fix:** Replace with `catch (JsonException)` (and optionally `catch (BadHttpRequestException)`).

#### Backend-26 · [LOW] Dead / no-op variable assignment in ParseFlowSummaries — always evaluates root regardless of branch

- **Dosya:** `src/Invekto.Backend/Program.cs` (4738)
- **Boyut:** simplification · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** In ParseFlowSummaries, the assignment `var flows = root.ValueKind == JsonValueKind.Array ? root : root;` is a tautology: both branches assign root to flows. The intent was presumably to handle the case where the response is a top-level object wrapping an array (e.g., { "items": [...] }), but the else branch was never completed. The variable flows is always equal to root, so the ternary is dead code.
- **Kanıt:** `var flows = root.ValueKind == System.Text.Json.JsonValueKind.Array ? root : root;`
- **Önerilen fix:** Remove the ternary and use root directly: `var flows = root;`. If the service can also return a wrapped { "items": [...] } shape, implement the non-array path (e.g. root.GetProperty("items")) — but only after confirming the actual contract. Do not silently fall through to EnumerateArray() on a non-array root, which would throw.

#### Backend-27 · [LOW] X-CIB-SecretKey set on DefaultRequestHeaders instead of per-request HttpRequestMessage

- **Dosya:** `src/Invekto.Backend/Program.cs` (9051-9052)
- **Boyut:** security · **Fix-risk:** low · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** The WapCRM bridge at line 9052 sets the tenant's WapCRM secret via `client.DefaultRequestHeaders.Add("X-CIB-SecretKey", wapcrm.SecretKey)`. The repo's hard-fail pattern explicitly requires per-request secrets to be set on the HttpRequestMessage via TryAddWithoutValidation, never on DefaultRequestHeaders. Although `CreateClient()` (unnamed) returns a new HttpClient wrapper per call today — making bleed between concurrent requests unlikely — the DefaultRequestHeaders approach violates the sanctioned pattern. If the code is ever refactored to use a named/typed client (a common migration), the secret would immediately be shared across all concurrent tenant requests, leaking tenant A's secret to tenant B.
- **Kanıt:** `var client = httpClientFactory.CreateClient(); client.DefaultRequestHeaders.Add("X-CIB-SecretKey", wapcrm.SecretKey);`
- **Önerilen fix:** Use a per-request message: `using var req = new HttpRequestMessage(HttpMethod.Post, wapcrm.ApiUrl) { Content = content }; req.Headers.TryAddWithoutValidation("X-CIB-SecretKey", wapcrm.SecretKey); var response = await client.SendAsync(req);`. Remove the DefaultRequestHeaders.Add line.

#### Backend-28 · [LOW] Sync-over-async (.GetAwaiter().GetResult()) in ResolveOpsIdentity when INMA cache misses

- **Dosya:** `src/Invekto.Backend/Program.cs` (10023)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving
- **Sorun:** ResolveOpsIdentity is a synchronous function called from an async endpoint handler. When the INMA token cache misses (~5% of calls per the comment), it calls `introspectorOps.ValidateAsync(token).GetAwaiter().GetResult()`, which blocks a thread-pool thread. The comment justifies this with "no SynchronizationContext so no deadlock" — which is accurate — but thread-pool blocking under load degrades throughput. This is not a hard-fail by the repo's rules but is a latent correctness risk under high concurrency.
- **Kanıt:** `var introspectOpsResult = introspectorOps.ValidateAsync(token).GetAwaiter().GetResult();`
- **Önerilen fix:** Make ResolveOpsIdentity async (`Task<string>`) and await the ValidateAsync call. Update each of its two call sites (lines 9443 and 9897) to await the result.

#### Backend-29 · [LOW] UniqueViolation retry in RotateFirstTimeInTxAsync / RotateExistingInTxAsync propagates wrong error code on second failure

- **Dosya:** `src/Invekto.Backend/Services/LiwSettingsService.cs` (212-222, 265-274)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** Both private helpers catch `PostgresException when SqlState == UniqueViolation` on the first attempt and retry the repository call bare (no second try/catch). If the retry also throws — either another UniqueViolation (extremely unlikely but possible under heavy key-space exhaustion) or any other PostgresException (connection issue mid-retry inside an already-opened transaction) — the exception propagates to the outer `catch (NpgsqlException ex)` in `RotateApiKeyAsync` (line 192-196). That catch logs `ErrorCodes.DatabaseConnectionFailed` and returns HTTP 503, which is the wrong classification for a persistent uniqueness race: the tenant operator sees "database connection failed" instead of a meaningful conflict signal. The transaction is correctly rolled back in the outer catch, so no data corruption occurs; only the error code surfaced to the caller is wrong.
- **Kanıt:** `LiwSettingsService.cs line 220-221: bare retry `ensure = await _tlsRepo.EnsureRowExistsOrCreateWithKeyAsync(...)` with no surrounding try/catch. Outer catch at line 191-196 catches NpgsqlException and returns 503 DatabaseConnectionFailed.`
- **Önerilen fix:** Wrap the retry call in its own catch block: if the retry also throws UniqueViolation, roll back and return `RotateApiKeyResult.Conflict()` (same as the race-lost path). Other exceptions should re-throw to the outer NpgsqlException catch. Apply the same pattern to `RotateExistingInTxAsync` lines 272-273.

#### Backend-30 · [LOW] TokenEqualsConstantTime leaks verify-token length via early return — contradicts constant-time claim

- **Dosya:** `src/Invekto.Backend/Services/MetaLeadgen/MetaLeadgenEndpoints.cs` (669-676)
- **Boyut:** security · **Fix-risk:** low · behavior-preserving
- **Sorun:** The helper's comment (line 665-667) explicitly claims 'timing attacks can't probe token length/content', but line 672 `if (a.Length != b.Length) return false;` returns immediately on length mismatch, leaking the length of the stored VerifyToken via response-time difference. An attacker who can hit the public GET handshake endpoint repeatedly can binary-search the token length before brute-forcing the content. The risk is lower than for HMAC keys (the VerifyToken is used only for Meta subscription setup, not ongoing request auth), but it directly contradicts the design contract. The sister class MetaLeadgenSignatureValidator correctly avoids this by using CryptographicOperations.FixedTimeEquals on fixed-length HMAC digests.
- **Kanıt:** `Line 671: `if (a is null) return false;` / Line 672: `if (a.Length != b.Length) return false;` — the length branch fires before any character comparison.`
- **Önerilen fix:** Encode both `a` and `b` to UTF-8 byte arrays of equal max-length (pad the shorter), then compare with `CryptographicOperations.FixedTimeEquals`. Alternatively, HMAC-expand both sides: `var key = RandomNumberGenerator.GetBytes(32); var ha = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(a ?? "")); var hb = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(b)); return CryptographicOperations.FixedTimeEquals(ha, hb);` — this is the canonical pattern used in ASP.NET Core's DataProtection.

#### Backend-31 · [LOW] Broad catch(Exception) in DetectLanguageCodeAsync outer handler lacks INV-xxx error code

- **Dosya:** `src/Invekto.Backend/Services/TranslationService.cs` (312-315)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The outer catch(Exception) in DetectLanguageCodeAsync degrades gracefully to the heuristic detector, which is the correct behavior. However it uses an inline error string "INV-TRANS-DETECT" that is not in arch/errors.md (confirmed by the code referencing ErrorCodes constants elsewhere in the same file for legitimate codes). This makes the error untrackable via the standard error-code system. The catch is also broad — OperationCanceledException from the caller CancellationToken is swallowed and a heuristic language guess is returned instead of propagating cancellation.
- **Kanıt:** `_logger.StepError($"[INV-TRANS-DETECT] AI detection failed, using heuristic: {ex.Message}", "-");`
- **Önerilen fix:** Add `OperationCanceledException oce when ct.IsCancellationRequested => throw;` before the broad catch, and replace the inline error string with an ErrorCodes constant (e.g., ErrorCodes.BackendTranslationDetectFailed) defined in arch/errors.md.

#### Backend-32 · [LOW] Dead _logger field injected but never used in all three repositories

- **Dosya:** `src/Invekto.Backend/Services/AnalyticsRepository.cs` (16, 21 (AnalyticsRepository); 16, 21 (AttributionRepository); 16, 21 (MessageLogRepository))
- **Boyut:** duplication · **Fix-risk:** low · behavior-preserving
- **Sorun:** All three repositories declare `private readonly JsonLinesLogger _logger` and assign it in the constructor, but the field is never called in any method body across all three files. This forces the DI container to resolve and inject a logger that is never consumed. In addition to dead-code noise, it means any future error paths silently drop log output (callers use fire-and-forget ContinueWith for InsertAsync, but no in-repo logging for NpgsqlException paths exists).
- **Kanıt:** `AnalyticsRepository.cs lines 16+21: field declared, assigned, zero usages in 606 lines. AttributionRepository.cs lines 16+21: same, zero usages in 457 lines. MessageLogRepository.cs lines 16+21: same, zero usages in 349 lines.`
- **Önerilen fix:** Either (a) remove `_logger` field and constructor parameter from all three classes if logging is genuinely not needed (callers handle errors via ContinueWith or let exceptions propagate), or (b) add targeted `_logger.SystemWarn(...)` calls in catch blocks when error handling is added. Option (a) is the correct behavior-preserving move given no errors are currently logged in-repo; removing unused DI injection is a net simplification.

### Outbound (17)

#### Outbound-1 · [CRITICAL] Internal opt-out and outbox-drain endpoints are unreachable: JWT middleware blocks all callers without a Bearer token

- **Dosya:** `src/Invekto.Outbound/Program.cs` (301, 1378-1449)
- **Boyut:** correctness · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** Outbound registers `UseJwtAuth` at line 301 with prefix `/api/v1/`, meaning every request to `/api/v1/internal/optout` and `/api/v1/internal/outbox/retry-skipped` must carry a valid `Authorization: Bearer` token or the JWT middleware returns 401 before the handler runs. Backend's callers (Program.cs lines 9422-9425 and 9449-9453) send only `X-Internal-Service-Token` — no `Authorization` header. The Outbound JWT middleware at JwtAuthMiddleware.cs lines 70-77 rejects any request missing a Bearer token with `401 Bearer token required`. The shared-secret check at Outbound lines 1386 and 1437 is therefore dead code and neither internal endpoint has ever been callable from Backend in production.
- **Kanıt:** `Outbound Program.cs line 301: `app.UseJwtAuth(jwtValidator, logger, "/api/v1/")`. JwtAuthMiddleware.cs line 70-77: rejects if no Bearer header, returns 401. Backend Program.cs line 9424: `client.DefaultRequestHeaders.Add("X-Internal-Service-Token", internalSharedSecret)` — no Authorization header ad…`
- **Önerilen fix:** Either (a) apply the same dual-auth pattern used in Backend (JWT required via middleware + shared-secret header inside the handler): Backend must mint a service-scoped JWT (with tenant_id) and attach `Authorization: Bearer <token>` alongside `X-Internal-Service-Token`, matching how other internal endpoints work; or (b) add `/api/v1/internal/` to the `authExcludedPrefixes` in Outbound's `UseJwtAuth` call, switch to the three-argument overload, and enforce auth exclusively via the shared-secret header — but then you lose the tenant-binding guarantee of the JWT. Option (a) is the cleaner fix and matches the existing LIW Chunk B pattern documented in IntakeInternalAuth.cs.

#### Outbound-2 · [HIGH] Null-forgiving operator (!) on ExecuteScalarAsync result — hard fail

- **Dosya:** `src/Invekto.Outbound/Data/OutboundRepository.cs` (353)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** IsBroadcastCompleteAsync casts the ExecuteScalarAsync return value with a null-forgiving operator: `(long)(await cmd.ExecuteScalarAsync(ct))!`. The `!` suffix is the C# null-forgiving operator, which is a repo hard-fail. ExecuteScalarAsync can theoretically return null (empty result set), so the cast would throw NullReferenceException at runtime rather than being caught as a typed exception. The correct approach is an explicit null guard or pattern match.
- **Kanıt:** `var count = (long)(await cmd.ExecuteScalarAsync(ct))!;`
- **Önerilen fix:** Replace with an explicit null guard: `var raw = await cmd.ExecuteScalarAsync(ct); var count = raw is null ? 0L : (long)raw;` — or equivalently `var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));` which handles DBNull and null safely. Remove the `!` operator entirely.

#### Outbound-3 · [HIGH] COUNT(cv.id) returns bigint — GetInt32 will throw InvalidCastException at runtime

- **Dosya:** `src/Invekto.Outbound/Data/OutboundRepository.cs` (1603,1619)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** GetCampaignRoiAsync executes `COUNT(cv.id)` (column index 4 in the SELECT). PostgreSQL COUNT always returns bigint (int8). The reader reads it with `reader.GetInt32(4)` which calls the Npgsql typed accessor for int4. Npgsql strict-typed accessors throw InvalidCastException when the column type is int8, not int4. This will crash at runtime as soon as the campaign has any conversion rows (LEFT JOIN produces at least one row per group). The same query also reads `COALESCE((c.stats_json->>'sent')::int, 0)` at ordinal 2 and 3, which are correctly cast to int in SQL, so those are fine.
- **Kanıt:** `COUNT(cv.id),  /* line 1603, SQL ordinal 4 */  ...  var totalConversions = reader.GetInt32(4);  /* line 1619 */`
- **Önerilen fix:** Cast COUNT to int in SQL: change `COUNT(cv.id)` to `COUNT(cv.id)::int` in the SELECT. Alternatively change `reader.GetInt32(4)` to `(int)reader.GetInt64(4)` on the C# side. The SQL-cast approach is consistent with the pattern already used for stats_json columns in the same query.

#### Outbound-4 · [HIGH] Project stuck in 'running' / auto-completes with 0 sends when ConfirmAsync fails after SetRunningAsync

- **Dosya:** `src/Invekto.Outbound/Services/ProjectsService.cs` (428-432)
- **Boyut:** correctness · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** In ConfirmSendAsync, SetRunningAsync (line 428) marks the project 'running' BEFORE _bulkOrch.ConfirmAsync (line 431). If ConfirmAsync returns a non-DB error — most critically BulkSendCapExceeded (when the operator-configured cap was lowered between preview and confirm, emitted in BulkSendOrchestrator.cs line 252 BEFORE the atomic TryClaimForConfirmAsync claim) — the project stays 'running' but the job remains 'preview_ready'. The RecomputeRollupAsync rollup counts ALL bulk_send_jobs (including preview_ready ones) in its 'runs' CTE, so run_count >= 1; with zero broadcasts and zero queued messages, the rollup transitions the project from 'running' to 'completed' (RecomputeRollupAsync line 418: ELSE 'completed'), stamps completed_at, and the project shows as 'completed' with 0 sends. The SS-D gate (line 418 in ConfirmSendAsync, checking for Running/Paused) then blocks any subsequent re-confirm from succeeding without Cancel first. The BulkSendNoValidRecipients path (post-claim) has the same effect: job is finalized to 'failed', project auto-completes to 'completed' with 0 sends, creating a confusing lifecycle state.
- **Kanıt:** `Line 428: `if (job.Status == "preview_ready" && !await _repo.SetRunningAsync(tenantId, projectId, ct))`. Line 431: `var (status, errCode, errMsg) = await _bulkOrch.ConfirmAsync(...)`. Line 432: `if (errCode != null) return (null, errCode, errMsg);` — no status rollback. Rollup CTE runs AS: `WHEN run…`
- **Önerilen fix:** After line 432 (errCode != null branch), before returning, add a best-effort rollback of the project status to 'draft': `await _repo.ResetToDraftAsync(tenantId, projectId, CancellationToken.None);`. Add `ResetToDraftAsync` to ProjectsRepository: `UPDATE projects SET status='draft', started_at=NULL, completed_at=NULL, updated_at=NOW() WHERE tenant_id=@tid AND id=@pid AND archived_at IS NULL AND status='running'` (the status='running' guard makes it safe to call on an already-resumed run — only reverts the specific SetRunning we just issued). The NpgsqlException from this rollback should be logged at SystemWarn (swallowed — best effort, not re-thrown) to avoid masking the original ConfirmAsync error.

#### Outbound-5 · [HIGH] GetJobMetaAsync reads nullable template_id with GetInt32 — InvalidCastException for HSM/inline jobs

- **Dosya:** `src/Invekto.Outbound/Data/ExportRepository.cs` (218-232)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving
- **Sorun:** bulk_send_jobs.template_id is nullable (arch/db/outbound.sql line 275: INTEGER with no NOT NULL, enforced by chk_bulk_message_source 3-way XOR). For HSM jobs (wa_template_id set) and inline-text jobs (inline_message_text set), template_id is NULL. GetJobMetaAsync calls reader.GetInt32(3) unconditionally at line 223 without an IsDBNull guard. Npgsql throws InvalidCastException on a DB NULL read via GetInt32 — not NpgsqlException — so it escapes the catch (NpgsqlException) block in ExportService.BuildSendReportDataAsync and ExportSendRecipientsAsync, surfacing as an unhandled 500. The field is also used downstream at ExportService line 219: GetTemplateNameAsync(tenantId, job.TemplateId, ct) — even if the exception were caught, passing 0 would silently look up the wrong template. JobExportMeta.TemplateId (line 61) is declared as non-nullable int, cementing the mismatch with the schema.
- **Kanıt:** `Line 223: `TemplateId = reader.GetInt32(3),` — no IsDBNull check. arch/db/outbound.sql line 275: `template_id INTEGER REFERENCES outbound_templates(id),` (nullable). chk_bulk_message_source (line 314) makes exactly one of template_id/inline_message_text/wa_template_id non-null per row.`
- **Önerilen fix:** Change JobExportMeta.TemplateId (line 61) to int?. Replace `reader.GetInt32(3)` with `reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3)`. In ExportService.BuildSendReportDataAsync (line 219), guard: `var templateName = job.TemplateId.HasValue ? await _repo.GetTemplateNameAsync(tenantId, job.TemplateId.Value, ct) : null;` Update SendReportSummary population accordingly.

#### Outbound-6 · [HIGH] ListRecentJobsAsync reads nullable template_id with GetInt32 — InvalidCastException for HSM/inline jobs

- **Dosya:** `src/Invekto.Outbound/Data/ExportRepository.cs` (352-383)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving
- **Sorun:** ListRecentJobsAsync selects template_id at ordinal 3 and reads it at line 376 with reader.GetInt32(3) without an IsDBNull guard. The same nullable column issue applies: any HSM or inline-text job in the result set causes InvalidCastException. This method is called from ExportService in two places: ListSendJobsAsync (line 92, the campaign picker endpoint) and ListFilterOptionsAsync (line 302, the filter-options dropdown). Both callers only catch NpgsqlException, so the InvalidCastException propagates as an unhandled 500. Since the filter-options endpoint is used on every page load of the Export Manager UI, the presence of a single HSM job breaks the entire Export Manager for that tenant. Shared DTO SendJobSummary.TemplateId (Invekto.Shared/DTOs/Outbound/ExportDtos.cs line 112) is also non-nullable int, compounding the mismatch.
- **Kanıt:** `Line 376: `TemplateId = reader.GetInt32(3),` — no IsDBNull check. ExportService line 302 calls this method and only catches NpgsqlException. Shared DTO: `public int TemplateId { get; set; }` (ExportDtos.cs line 112).`
- **Önerilen fix:** Change SendJobSummary.TemplateId in Invekto.Shared/DTOs/Outbound/ExportDtos.cs to int?. Replace `reader.GetInt32(3)` with `reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3)`. In ExportService.ListFilterOptionsAsync (line 303), the job.Id/job.CampaignId mapping is unaffected. In ListSendJobsAsync the TemplateId is passed through to the API client — the SPA already needs to handle null for HSM jobs so the field type change is consistent.

#### Outbound-7 · [HIGH] SendMessageAsync swallows OperationCanceledException via broad catch(Exception)

- **Dosya:** `src/Invekto.Outbound/Services/MessageSenderService.cs` (308-319)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** The single catch(Exception ex) at line 308 has no when-filter for OperationCanceledException. When the CancellationToken fires during graceful shutdown — inside SendCallbackAsync (line 278), any subsequent UpdateMessageStatusAsync, IncrementBroadcastCounterAsync, or TryCompleteBroadcastAsync — the cancellation is caught here instead of propagating to ProcessQueue's catch(OperationCanceledException) at line 174. The message is then marked 'failed' (line 311-312) and the broadcast counter is incremented (lines 316-317) under a cancelled token, which can itself throw a second OperationCanceledException from the DB calls. Per repo policy, broad catch(Exception) is forbidden and every exception must have a typed catch plus an INV-xxx error code.
- **Kanıt:** `catch (Exception ex) { _logger.SystemError($"SendMessage exception: ..."); await _repository.UpdateMessageStatusAsync(msg.Id, "failed", ...) }`
- **Önerilen fix:** Add catch(OperationCanceledException) { throw; } BEFORE the broad catch, so shutdown cancellation re-throws to ProcessQueue's handler. Then narrow the remaining catch to catch(NpgsqlException ex) for DB failures (tagged INV-OB-xxx) and catch(HttpRequestException ex) for callback transport failures, each with their own INV error code in the log and failed_reason. Or at minimum add: catch (OperationCanceledException) { throw; } before the existing catch(Exception ex), and add an INV code to the log line.

#### Outbound-8 · [HIGH] ConsentManager batch consent check silently allows all phones when none are in the consent table

- **Dosya:** `src/Invekto.Outbound/Services/ConsentManager.cs` (92-101)
- **Boyut:** correctness · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** GetPhonesWithoutMarketingConsentAsync returns an empty set (skip nobody) when BatchCheckMarketingConsentAsync returns zero opted-in phones. The comment acknowledges this is ambiguous: it could mean the tenant has no consent records (backwards-compat case, allow all) OR every phone is explicitly opted-out. When a tenant has a populated consent_records table but every batch phone has opted-out=TRUE with type 'marketing'/'all', BatchCheckMarketingConsentAsync still returns zero, and the batch check treats all those phones as having consent — silently sending to opted-out recipients. The per-recipient fallback HasMarketingConsentAsync is only used by TriggerProcessor, not by BroadcastOrchestrator's batch path.
- **Kanıt:** `if (consentedPhones.Count == 0) { // Could be: (a) tenant doesn't use consent system, or (b) all opted out. // ... assume consent system not yet configured → skip nobody. return new HashSet<string>(); }`
- **Önerilen fix:** Add a separate lightweight query that checks whether the tenant has ANY consent_records rows at all (e.g. EXISTS(SELECT 1 FROM consent_records WHERE tenant_id=@tid LIMIT 1)). If that returns true, return ALL phones as no-consent when BatchCheck returns empty (the 'all opted out' case). If it returns false, return empty (backwards-compat, no consent records). Alternatively, rename the current behaviour to explicit opt-in mode only and document that backwards-compat is governed by a feature flag.

#### Outbound-9 · [MEDIUM] Data deletion partial-state: if UpdateDeletionRequestAsync throws after data is deleted, the deletion_request row is stuck as 'pending' forever

- **Dosya:** `src/Invekto.Outbound/Program.cs` (1908-1946)
- **Boyut:** error-handling · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR · _(orijinal sev: high → düzeltildi)_
- **Sorun:** The handler executes three sequential DB calls: (1) CreateDeletionRequestAsync — inserts a 'pending' row, (2) ExecuteDataDeletionAsync — irreversibly deletes PII, (3) UpdateDeletionRequestAsync — marks the row 'completed'. If step 3 throws `NpgsqlException`, the catch block at line 1933 returns 500 but makes no attempt to mark the deletion_request row as 'failed'. The PII is already gone (step 2 succeeded) but the audit row remains perpetually 'pending'. There is also no attempt to mark the row 'failed' if step 2 throws — the row is created then stuck as 'pending' with no indication of failure in the audit table.
- **Kanıt:** `Lines 1910-1917: three sequential awaits with no compensation on failure. Lines 1933-1938: catch(NpgsqlException) only logs and returns 500 — no UpdateDeletionRequestAsync call with status='failed'. Lines 1940-1945: same for InvalidOperationException.`
- **Önerilen fix:** Add a try/finally or a dedicated failure path: if step 2 or 3 throws after step 1 has succeeded, call `await repository.UpdateDeletionRequestAsync(tenantContext.TenantId, deletionId, "failed", ..., ex.Message, CancellationToken.None)` in the catch block (wrapped in its own try/catch to avoid double-fault). This keeps the audit table consistent even when the final commit fails. A secondary improvement: move step 2 inside the same DB transaction as step 3 if the repository supports it, so data deletion and audit update are atomic.

#### Outbound-10 · [MEDIUM] TryCompleteBroadcastAsync swallows OperationCanceledException and has no INV error code

- **Dosya:** `src/Invekto.Outbound/Services/MessageSenderService.cs` (572-575)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** TryCompleteBroadcastAsync wraps IsBroadcastCompleteAsync + UpdateBroadcastStatusAsync in a catch(Exception ex) with no when-filter and no INV-xxx code. This method is called from SendMessageAsync (lines 290, 301, 317), SendViaCxapiAsync (lines 354, 375, 425, 487), and TryRecoverStrandedAsync (line 210). When the cancellation token fires, an OperationCanceledException thrown by the DB calls is silently logged as a generic error and discarded. The shutdown path (StopAsync) already waits up to 10 seconds for _isProcessing; swallowing the cancellation inside TryCompleteBroadcastAsync delays this wait unnecessarily and masks the actual cause. No INV error code is emitted, so broadcast-completion failures are invisible to the error-code monitoring layer.
- **Kanıt:** `catch (Exception ex) { _logger.SystemError($"Error checking broadcast completion: {broadcastId}, {ex.Message}"); }`
- **Önerilen fix:** Replace with: catch (OperationCanceledException) { throw; } catch (NpgsqlException ex) { _logger.SystemError($"[{ErrorCodes.SomeExistingBroadcastErrorCode}] Error checking broadcast completion: {broadcastId}, {ex.Message}"); } — pick the nearest INV code (e.g. the one used for broadcast update failures) from arch/errors.md. Since TryCompleteBroadcastAsync is a best-effort helper, the NpgsqlException case can remain swallowed; only the OperationCanceledException must re-throw.

#### Outbound-11 · [MEDIUM] ProcessQueue outer catch(Exception) has no INV error code

- **Dosya:** `src/Invekto.Outbound/Services/MessageSenderService.cs` (178-181)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: low → düzeltildi)_
- **Sorun:** The outermost catch in ProcessQueue (line 178) is a last-resort background-timer handler. Its purpose (prevent the timer callback from dying on an unexpected exception) is architecturally sound for an async void Timer callback, and the prior catch(OperationCanceledException) at line 174 already handles the normal shutdown path. However, per repo policy all catch(Exception) blocks must include an INV-xxx error code. The log line 'MessageSenderService error: {ex.Message}' emits no error code, making unexpected send-loop failures invisible to the structured-log monitoring layer.
- **Kanıt:** `catch (Exception ex) { _logger.SystemError($"MessageSenderService error: {ex.Message}"); }`
- **Önerilen fix:** Add an INV error code to the log: _logger.SystemError($"[{ErrorCodes.SomeGenericWorkerError}] MessageSenderService error: {ex.Message}"); — choose the most appropriate existing constant from arch/errors.md (e.g. the outbound worker error code if one exists). The broad catch itself is acceptable here as a background last-resort handler; only the INV code annotation is missing.

#### Outbound-12 · [MEDIUM] PreviewAsync (CSV path) has no NpgsqlException catch — asymmetric with PreviewFromListAsync

- **Dosya:** `src/Invekto.Outbound/Services/BulkSendOrchestrator.cs` (87-115)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** PreviewFromListAsync (lines 153-222) wraps all DB calls in a NpgsqlException catch that returns (null, ErrorCodes.ContactListDbError, "Database error... retry.") — a typed 503. PreviewAsync (the CSV path) makes three awaited DB calls — GetTemplateByIdAsync (line 87), GetJobAsync (line 92), CreatePreviewJobAsync (line 111) — with no equivalent try/catch. A transient Postgres connection failure on any of those three calls propagates as an unhandled NpgsqlException to the controller, which surfaces as an opaque 500 instead of a typed, retryable INV error response. This asymmetry means the same underlying fault (transient DB) produces different HTTP responses depending on which preview path the caller used.
- **Kanıt:** `var template = await _repository.GetTemplateByIdAsync(tenantId, request.TemplateId, ct); // no surrounding try/catch in PreviewAsync`
- **Önerilen fix:** Wrap the three DB calls in PreviewAsync (lines 87-115) in a try/catch(NpgsqlException ex) that mirrors PreviewFromListAsync: log the error with an INV code and return (null, ErrorCodes.BulkSendDispatchFailed or a dedicated BulkSendDbError code, "Database error; retry."). The guard on lines 72-84 (input validation, feature flag) can stay outside the try since they are pure/non-DB checks.

#### Outbound-13 · [MEDIUM] CampaignOrchestrator DB calls have no NpgsqlException handling — exceptions surface as untyped 500

- **Dosya:** `src/Invekto.Outbound/Services/CampaignOrchestrator.cs` (38-156)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** CreateCampaignAsync, ActivateCampaignAsync, and RecordConversionAsync all make DB calls (_repository.GetTemplateByIdAsync, CreateCampaignAsync, GetCampaignAsync, etc.) with no NpgsqlException catch. Every other service in the same codebase (ContactListImportService, ExportService, ProjectsService) wraps its DB calls in typed NpgsqlException catch blocks that map to INV error codes and return clean user-facing errors. A transient DB failure in any CampaignOrchestrator method will propagate as an unhandled exception and surface as an opaque 500 at the endpoint.
- **Kanıt:** `public async Task<(CampaignResponse? response, ...)> CreateCampaignAsync(...) { ... var template = await _repository.GetTemplateByIdAsync(tenantId, request.TemplateId, ct); // no catch ... var id = await _repository.CreateCampaignAsync(...); // no catch }`
- **Önerilen fix:** Wrap each public method's DB calls in a try/catch (NpgsqlException ex) block and return (null, ErrorCodes.OutboundCampaignDbError, "...") — matching the pattern in ContactListImportService and ProjectsService. A new INV-OB-xxx error code should be registered in arch/errors.md if one does not already exist for campaign DB failures.

#### Outbound-14 · [LOW] Shared-secret comparison is timing-vulnerable: plain `!=` instead of constant-time compare

- **Dosya:** `src/Invekto.Outbound/Program.cs` (1386, 1437)
- **Boyut:** security · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** Both internal-endpoint handlers compare the provided shared secret with `providedSecret != internalSharedSecret` — a plain string equality check that short-circuits on the first differing character. This enables a remote timing attack to brute-force the shared secret one character at a time. The canonical pattern in this codebase is `IntakeInternalAuth.SlowEquals` (IntakeInternalAuth.cs lines 52-58), which performs a constant-time XOR comparison. The Outbound internal handlers do not use this utility.
- **Kanıt:** `Line 1386: `if (string.IsNullOrEmpty(internalSharedSecret) || providedSecret != internalSharedSecret)`. Line 1437: same pattern. IntakeInternalAuth.cs line 56-58: `int diff = 0; for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i]; return diff == 0;``
- **Önerilen fix:** Extract `IntakeInternalAuth.SlowEquals` (or an equivalent) to `Invekto.Shared` (since Outbound cannot reference Backend's assembly) and call it in both checks. Alternatively, inline the same XOR loop. Replace both `providedSecret != internalSharedSecret` with the constant-time variant. Also add the early-exit guard for mismatched length that the existing SlowEquals already includes.

#### Outbound-15 · [LOW] Duplicated `validEvents` array allocated fresh on every POST and PUT template request

- **Dosya:** `src/Invekto.Outbound/Program.cs` (1492-1494, 1536-1538)
- **Boyut:** duplication · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The allowed trigger_event string set is declared twice as a `new[]` array literal (lines 1492 and 1536) inside two separate request handlers. Each request allocates a new array and performs a linear O(n) `Contains` scan. If the two copies drift (one is updated and the other is not), POST and PUT will enforce different valid-event sets silently. The two arrays are already slightly inconsistent in indentation, making them a copy-paste maintenance risk.
- **Kanıt:** `Line 1492-1494: `var validEvents = new[] { "manual", "new_lead", ... }` inside POST handler. Line 1536-1538: identical array inside PUT handler. Identical values today but no single source of truth.`
- **Önerilen fix:** Declare a single `static readonly HashSet<string> ValidTriggerEvents = new(StringComparer.Ordinal) { "manual", "new_lead", ... }` at file/program scope (top-level statements support this) and reference it in both handlers. HashSet.Contains is O(1); this also eliminates the per-request allocation.

#### Outbound-16 · [LOW] ConfirmSendAsync project-run-in-progress check uses a stale reload, not the just-claimed project status

- **Dosya:** `src/Invekto.Outbound/Services/ProjectsService.cs` (374-418)
- **Boyut:** correctness · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** The active-run guard at line 418 checks `detail.Project.Status` which was loaded at line 374 from a SELECT (non-atomic with the subsequent SetRunningAsync UPDATE). Between the load at line 374 and the check at line 418, another confirm could have raced in on the same project (concurrent confirm on a preview_ready job). If two concurrent confirms both load 'draft' at line 374, both pass the gate at line 418, both call SetRunningAsync (which is idempotent to 'running'), and both call _bulkOrch.ConfirmAsync — only one wins the TryClaimForConfirmAsync atomic claim, but both marks the project as 'running'. The loser's ConfirmAsync returns the live status (idempotent re-confirm path at BulkSendOrchestrator line 239-240), which is fine for the return. But this means SetRunningAsync is called twice for a single dispatch, making the 'started_at' stamp non-deterministic. Low severity because TryClaimForConfirmAsync prevents double-dispatch, but SetRunningAsync lacks a `status='draft'` guard that would make it idempotent only on a true new-dispatch.
- **Kanıt:** `Line 374: `var detail = await _repo.GetAsync(...)`. Line 418: `if (job.Status == "preview_ready" && detail.Project.Status is ProjectStatuses.Running or ProjectStatuses.Paused)`. Line 428: `await _repo.SetRunningAsync(...)` — no CAS guard on prior status.`
- **Önerilen fix:** Add `AND status NOT IN ('running','paused','cancelled','archived')` to SetRunningAsync's WHERE clause so it is a CAS transition (draft→running only). Return false if already-running/paused, letting ConfirmSendAsync detect the race and return an appropriate error before dispatching. Alternatively, move SetRunningAsync to AFTER the successful TryClaimForConfirmAsync in BulkSendOrchestrator (but that crosses service boundaries).

#### Outbound-17 · [LOW] StopAsync catch(Exception) wrapping DB-only call — swallows OperationCanceledException, no INV code

- **Dosya:** `src/Invekto.Outbound/Services/MessageSenderService.cs` (99-106)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** StopAsync wraps ResetSendingMessagesAsync — a Postgres-only call — in catch(Exception ex) with no when-filter and no INV-xxx error code (line 103). The only realistic runtime exceptions are NpgsqlException (transient DB) and OperationCanceledException (the ASP.NET shutdown token passed in). Swallowing OperationCanceledException here is harmless in practice (shutdown is already in progress), but it violates the repo policy and masks the true cause in logs. The absence of an INV error code means the failure is invisible to monitoring.
- **Kanıt:** `catch (Exception ex) { _logger.SystemError($"Failed to reset stale sending messages: {ex.Message}"); }`
- **Önerilen fix:** Narrow to catch(NpgsqlException ex) and add an INV-xxx error code to the log line. OperationCanceledException from the stop-path can be silently discarded (the service is stopping anyway) by adding catch(OperationCanceledException) { } before the NpgsqlException handler, or by passing CancellationToken.None to ResetSendingMessagesAsync since the call must complete even during shutdown.

### Automation (27)

#### Automation-1 · [HIGH] broad catch(Exception) in SyncFlowInstanceMappingAsync — repo hard-fail pattern

- **Dosya:** `src/Invekto.Automation/Data/AutomationRepository.cs` (643-647)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** SyncFlowInstanceMappingAsync catches bare catch(Exception) before re-throwing. The repo hard-fail rule forbids this even when the exception is immediately re-thrown — it must be a typed catch (NpgsqlException) so that non-DB exceptions (e.g. OperationCanceledException from CancellationToken) are not intercepted and rolled back as if they were DB errors. As written, a task cancellation will trigger the RollbackAsync path unnecessarily.
- **Kanıt:** `catch (Exception) ⏎ { ⏎     await tx.RollbackAsync(ct); ⏎     throw; ⏎ }`
- **Önerilen fix:** Replace with: catch (NpgsqlException) { await tx.RollbackAsync(ct); throw; }  If rollback on OperationCanceledException is also desired, add a separate catch (OperationCanceledException) { await tx.RollbackAsync(ct); throw; } — but keep them typed. The current broad catch also silently captures future unrelated exceptions.

#### Automation-2 · [HIGH] SSRF DNS rebinding race in ApiCallHandler

- **Dosya:** `src/Invekto.Automation/Services/NodeHandlers/ApiCallHandler.cs` (248-264)
- **Boyut:** security · **Fix-risk:** medium · behavior-preserving
- **Sorun:** DNS is resolved at validation time (line 251 Dns.GetHostAddressesAsync), but HttpClient.SendAsync performs its own independent DNS lookup when the request is dispatched. An attacker-controlled DNS server can return a public IP at validation time (passing the check) and then switch the record to 127.0.0.1 before the actual HTTP connection, bypassing the SSRF guard entirely.
- **Kanıt:** `var addresses = await Dns.GetHostAddressesAsync(host); ... return null; // Safe  — then later: using var response = await httpClient.SendAsync(request, ...) — two separate DNS lookups with no pinning.`
- **Önerilen fix:** Resolve DNS once in ValidateUrlSsrf, validate the returned IPs, then rewrite the request URL to use the resolved IP directly (e.g. replace host with ip.ToString()) and set the Host header to the original hostname. Alternatively, configure the HttpClient with a custom connection callback (SocketsHttpHandler.ConnectCallback) that enforces the pre-validated IP. This eliminates the TOCTOU window entirely.

#### Automation-3 · [MEDIUM] Broad catch(Exception) in fire-and-forget Task.Run — hard-fail pattern

- **Dosya:** `src/Invekto.Automation/Program.cs` (569-574)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** The background Task.Run block inside the POST /api/v1/webhook/event handler catches `catch (Exception ex)` with no typed alternatives and no INV-XXX error code mapping. Per repo hard-fail rules, broad catch(Exception) is forbidden even without a when(...) filter; must be typed catches with an error-code mapping. Any NpgsqlException, OperationCanceledException, or domain error lands here with no differentiation — NpgsqlExceptions silently trigger the error-callback path instead of being surfaced as database errors.
- **Kanıt:** `catch (Exception ex) { jsonLogger.StepError($"Background processing exception: {ex.Message}", requestId); await SendErrorCallbackAsync(...); }`
- **Önerilen fix:** Replace with typed catches: `catch (OperationCanceledException) { /* log + ignore, request already abandoned */ }` then `catch (Npgsql.NpgsqlException dbEx) { jsonLogger.StepError($"[{ErrorCodes.DatabaseConnectionFailed}] Background DB error: {dbEx.Message}", requestId); await SendErrorCallbackAsync(...); }` then `catch (InvalidOperationException domEx) { ... INV-AT-xxx ... }`. Reserve a final narrow catch only if there is a documented reason, mapped to a specific INV-xxx code.

#### Automation-4 · [MEDIUM] Silent bare catch { } inside LINQ projection — swallows non-JSON errors

- **Dosya:** `src/Invekto.Automation/Program.cs` (684)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** A completely empty bare `catch { /* ignore parse errors for description extraction */ }` wraps the description extraction block. The comment says 'parse errors' but the catch is untyped — it will also silently swallow NullReferenceException, ObjectDisposedException, or any other exception thrown inside the using block. If `JsonDocument.Parse` succeeds but a downstream call throws, the error is invisible.
- **Kanıt:** `catch { /* ignore parse errors for description extraction */ }`
- **Önerilen fix:** Replace with `catch (System.Text.Json.JsonException) { /* expected: malformed config */ }`. Any other exception type should not be silenced.

#### Automation-5 · [MEDIUM] Multiple endpoint outer handlers missing NpgsqlException before broad catch(Exception)

- **Dosya:** `src/Invekto.Automation/Program.cs` (705-709, 809-813, 940-944, 969-973, 1020-1024, 1045-1049, 1202-1206, 1249-1253, 1445-1449, 1508-1512, 1543-1547, 1584-1588, 1627-1631, 1649-1653, 1693-1696)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** Fourteen endpoint handlers use a broad `catch (Exception ex)` as their final/only catch, mapping to `ErrorCodes.GeneralUnknown`. Per repo rules, DB catch must catch `NpgsqlException` (base) before the broad catch so DB errors receive `ErrorCodes.DatabaseConnectionFailed`. In these handlers a Npgsql connection failure returns HTTP 500 with error code `INV-GEN-001` (GeneralUnknown) instead of the database-specific code, making error classification and alerting impossible. Affected handlers include: GET /flows/{tenantId} outer, POST /flows/{tenantId}, PUT /flows/{tenantId}/{flowId} outer, DELETE /flows/{tenantId}/{flowId}, POST activate, POST deactivate, POST /flows/validate, POST migrate-v1, POST /simulation/start, POST /simulation/step, GET /faq/{tenantId}, POST /faq, PUT /faq, DELETE /faq, POST /onboarding/seed-intents.
- **Kanıt:** `catch (Exception ex) { jsonLogger.StepError($"Flow list failed: {ex.Message}", "-"); return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", "-"), statusCode: 500); }`
- **Önerilen fix:** Before each broad `catch (Exception ex)` that returns a 500, add `catch (Npgsql.NpgsqlException dbEx) { jsonLogger.StepError($"[{ErrorCodes.DatabaseConnectionFailed}] ...: {dbEx.Message}", requestId); return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Veritabani hatasi", requestId), statusCode: 503); }`. Endpoints that already have a typed NpgsqlException handler at a narrower scope (e.g., unique-violation 23505) still need the base NpgsqlException catch in the outer handler.

#### Automation-6 · [MEDIUM] Culture-sensitive DateTime.Parse for query parameters in /returns/{tenantId}/stats

- **Dosya:** `src/Invekto.Automation/Program.cs` (1719-1720)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** `DateTime.Parse(from)` and `DateTime.Parse(to)` use the server's ambient culture. The production server is a Windows machine which could be configured with tr-TR locale. In tr-TR, date separator is `.` and month/day ordering differs — a client submitting `2024-01-15` may parse incorrectly or throw FormatException depending on locale. The same file uses `DateTime.TryParse(date_from, CultureInfo.InvariantCulture, ...)` correctly at lines 1350-1353 for the monitor endpoint. The inconsistency means the returns stats endpoint is not safe on non-en-US servers.
- **Kanıt:** `var fromDate = !string.IsNullOrEmpty(from) ? DateTime.Parse(from) : DateTime.UtcNow.AddDays(-30);`
- **Önerilen fix:** Replace both lines with the pattern already used at lines 1350-1353: `var fromDate = !string.IsNullOrEmpty(from) && DateTime.TryParse(from, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var df) ? df.ToUniversalTime() : DateTime.UtcNow.AddDays(-30);` (and same for `to`). Remove the `FormatException` catch as TryParse does not throw.

#### Automation-7 · [MEDIUM] GetReturnDeflectionStatsAsync reads COUNT(*) with GetInt32 — unsafe if row count > int.MaxValue

- **Dosya:** `src/Invekto.Automation/Data/AutomationRepository.cs` (1281-1325)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The SQL uses COUNT(*) without a ::int cast. PostgreSQL returns COUNT(*) as bigint. The code then calls reader.GetInt32(0) and reader.GetInt32(1) for the GROUPING SETS grand-total row (lines 1313-1314) and reader.GetInt32(0) for the per-group rows (1319, 1322). Without an explicit ::int cast the driver returns Int64 for those columns, and GetInt32 throws InvalidCastException at runtime when rows exist. The repo rule explicitly states 'COUNT(*) returns bigint -> GetInt32 throws InvalidCastException, must be ::int cast or GetInt64'.
- **Kanıt:** `SELECT COUNT(*) AS total, COUNT(*) FILTER ... AS deflected ... then: total = reader.GetInt32(0); deflected = reader.GetInt32(1); ... byReason[reasonCat] = reader.GetInt32(0);`
- **Önerilen fix:** Add ::int casts in the SQL: COUNT(*)::int AS total, COUNT(*) FILTER (WHERE was_deflected = TRUE)::int AS deflected — matching the pattern already correctly used in GetOnboardingStatsAsync (line 1378-1379) and ListExecutionLogsAsync (countSql). Alternatively change all reader.GetInt32 calls for count columns to reader.GetInt64 and update the return type accordingly.

#### Automation-8 · [MEDIUM] RollbackFlowVersionAsync: two separate connections for fetch + update — non-atomic TOCTOU

- **Dosya:** `src/Invekto.Automation/Data/AutomationRepository.cs` (511-529)
- **Boyut:** correctness · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** RollbackFlowVersionAsync first calls GetFlowVersionAsync (which opens connection A, reads config, closes it), then opens connection B to UPDATE chatbot_flows, then calls CreateFlowVersionAsync (which opens connection C for a new transaction). Between connections A and C another concurrent save could have modified chatbot_flows. The chatbot_flows UPDATE on connection B (line 521) is also not part of a transaction together with the CreateFlowVersionAsync call on connection C, so if CreateFlowVersionAsync fails after the UPDATE, chatbot_flows now has the old config but current_version was NOT updated — leaving data inconsistent.
- **Kanıt:** `var target = await GetFlowVersionAsync(...); // conn A ⏎ await using var conn = await _db.OpenConnectionAsync(ct); // conn B — no transaction ⏎ await cmd.ExecuteNonQueryAsync(ct); // bare UPDATE ⏎ return await CreateFlowVersionAsync(...); // conn C — own transaction`
- **Önerilen fix:** Merge all three steps into a single transaction on a single connection: (1) SELECT flow_config FROM flow_versions WHERE ... FOR UPDATE, (2) UPDATE chatbot_flows SET flow_config=..., (3) INSERT into flow_versions + UPDATE current_version. If any step fails, the whole transaction rolls back.

#### Automation-9 · [MEDIUM] Broad catch(Exception) in pre-flow enrichment block swallows cancellation

- **Dosya:** `src/Invekto.Automation/Services/AutomationOrchestrator.cs` (487-524)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** The try/catch around the parallel intent + settings fetch (line 521) uses bare `catch (Exception ex)`. This block awaits `Task.WhenAll(intentTask, settingsTask)` (line 508) where both tasks can involve async DB or HTTP calls. An `OperationCanceledException` from shutdown will be absorbed, the log will say 'Pre-flow enrichment failed' with the cancellation message, and the orchestrator will continue executing the flow with null intents and 0.5 confidence threshold rather than propagating cancellation upstream. Violates typed-catch repo rule (INV-XXX required).
- **Kanıt:** `Line 521: `catch (Exception ex) { _logger.SystemWarn($"[{ErrorCodes.AutomationKnowledgeIntentFetchFailed}] Pre-flow enrichment failed..."); }` — no typed discrimination, no OCE rethrow.`
- **Önerilen fix:** Add `catch (OperationCanceledException) { throw; }` before the broad catch, then narrow the remaining catch to `catch (HttpRequestException ex)` and `catch (NpgsqlException ex)` and `catch (InvalidOperationException ex)`, each tagged with the existing error code. The degraded-to-null behaviour is correct for transient errors but not for cancellation.

#### Automation-10 · [MEDIUM] Null-forgiving operators on row.Phone and result.AssignGroupId (repo hard-fail)

- **Dosya:** `src/Invekto.Automation/Services/AutomationOrchestrator.cs` (1117, 1152)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: low → düzeltildi)_
- **Sorun:** Two null-forgiving `!` operators remain in ResumeWaitAsync. Line 1117: `row.Phone!` is inside `else if (!string.IsNullOrEmpty(row.Phone))` so it is logically non-null, but the `!` operator suppresses compiler analysis rather than eliminating the nullable type from scope. Line 1152: `result.AssignGroupId!` is inside `!string.IsNullOrWhiteSpace(result.AssignGroupId)` guard — same pattern. Per repo rules, `!` is a hard-fail regardless of logical safety; use a local variable or pattern-bind instead.
- **Kanıt:** `Line 1117: `else if (!string.IsNullOrEmpty(row.Phone)) contactKey = row.Phone!;` / Line 1152: `result.AssignGroupId!, result.AssignGroupSummary ?? ...``
- **Önerilen fix:** Line 1117: assign to a local in the guard: `else if (row.Phone is { Length: > 0 } p) contactKey = p;` or use a local variable `var ph = row.Phone; if (!string.IsNullOrEmpty(ph)) contactKey = ph;`. Line 1152: `var gid = result.AssignGroupId; if (!string.IsNullOrWhiteSpace(gid)) { await SendAssignGroupAsync(..., gid, ...); }`.

#### Automation-11 · [MEDIUM] chatId passed as phone to CampaignTemplateApplier — locale lookup always misses

- **Dosya:** `src/Invekto.Automation/Services/AutomationOrchestrator.cs` (1483-1484)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** In SendCallbackAsync (which has no phone parameter), `chatId` is passed as the `phone` argument to `_campaignApplier.ApplyAsync` (line 1484: `phone: chatId`). Inside CampaignTemplateApplier.ApplyAsync (line 98), `phone` is used to call `_repo.GetLeadPreferredLocaleAsync(tenantId, phone!, ct)` which queries `WHERE phone = @phone` in the leads table. WhatsApp chatIds are stored as `491234567890@c.us` while the leads table stores plain phone numbers (`491234567890`). The lookup always returns null, so `resolvedLocale` always falls back to `"en"`, causing all campaign `{{campaign.X}}` placeholder substitutions to resolve in English regardless of the contact's detected language. This silently breaks multi-locale campaigns for all non-English tenants.
- **Kanıt:** `AutomationOrchestrator.cs line 1484: `_campaignApplier.ApplyAsync(tenantId, phone: chatId, ...)`. CampaignTemplateApplier.cs line 98: `await _repo.GetLeadPreferredLocaleAsync(tenantId, phone!, ct)`. AutomationRepository.cs line 948: `cmd.CommandText = "SELECT preferred_locale FROM leads WHERE tenant…`
- **Önerilen fix:** Add a `string? phone = null` parameter to `SendCallbackAsync` and thread the actual `phone` value from `ProcessV2MessageAsync` (and `ProcessMessageAsync` v1 path). Pass it through to `_campaignApplier.ApplyAsync(tenantId, phone: phone, ...)`. All existing callers that don't have phone context can pass `null`, which CampaignTemplateApplier already handles gracefully (locale defaults to 'en' as the explicit fallback, but will now correctly find the phone when available).

#### Automation-12 · [MEDIUM] Broad catch(Exception) in node handler dispatch — repo hard-fail violation

- **Dosya:** `src/Invekto.Automation/Services/FlowEngineV2.cs` (138-156)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** The OperationCanceledException is correctly re-thrown (line 138), but the fallthrough catch at line 142 is a bare catch(Exception). The repo policy states: 'broad catch(Exception) is forbidden EVEN with a when(...) filter — must be typed catch + an INV-XXX error code mapping.' The INV-XXX code (AutomationNodeExecutionFailed = INV-AT-021) is present, but the catch is still untyped. This swallows all exception types uniformly, including ThreadAbortException-equivalents and out-of-memory conditions, converting them into 'needs handoff' results rather than letting the host crash or retry appropriately.
- **Kanıt:** `catch (Exception ex) { _logger.SystemWarn($"[{ErrorCodes.AutomationNodeExecutionFailed}] Node {currentNodeId} ({node.Type}) execution failed: {ex.Message}"); state.Status = "error"; return new EngineStepResult { ... NeedsHandoff = true, ErrorCode = ErrorCodes.AutomationNodeExecutionFailed ... }; }`
- **Önerilen fix:** Replace with typed catches covering the realistic failure surface of pure handlers: catch (InvalidOperationException ex) / catch (ArgumentException ex) / catch (FormatException ex) / catch (JsonException ex), each mapping to ErrorCodes.AutomationNodeExecutionFailed with the same return block. Add a final rethrow for anything else. If the handler contract is widened later, add types then.

#### Automation-13 · [MEDIUM] GetInt32()/GetDouble() on JsonElement without ValueKind check — uncaught InvalidOperationException on malformed flow config

- **Dosya:** `src/Invekto.Automation/Services/FlowGraphV2.cs` (78, 183-187)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** Build(JsonDocument doc) at line 78 calls vProp.GetInt32() to verify the version field. If the stored JSON has 'version': '2' (a string, not a number) — which can happen if a frontend bug serializes it as string — GetInt32() throws InvalidOperationException, not JsonException. The Build(string) overload only catches JsonException (line 57-62), so this exception propagates uncaught to every caller of Build(JsonDocument) in AutomationOrchestrator, and to every caller of Build(string) in SimulationEngine, FlowValidator, and the job classes. ParseSettings (lines 183-187) has the same issue for handoff_confidence_threshold (GetDouble), session_timeout_minutes (GetInt32), and max_loop_count (GetInt32) — all unchecked ValueKind before typed accessor call.
- **Kanıt:** `if (!root.TryGetProperty("version", out var vProp) || vProp.GetInt32() != 2) // line 78\nsettings.HandoffConfidenceThreshold = hct.GetDouble(); // line 183\nsettings.SessionTimeoutMinutes = stm.GetInt32(); // line 185\nsettings.MaxLoopCount = mlc.GetInt32(); // line 187`
- **Önerilen fix:** For version: use TryGetProperty + check vProp.ValueKind == JsonValueKind.Number before GetInt32(), returning null on mismatch. Alternatively: `if (!root.TryGetProperty("version", out var vProp) || vProp.ValueKind != JsonValueKind.Number || vProp.GetInt32() != 2) return null;`. Apply the same pattern for the three settings fields (check ValueKind before calling typed accessor, skip/default on wrong kind). This keeps Build returning null for all bad-config paths rather than throwing.

#### Automation-14 · [MEDIUM] Null-forgiving operator on nullable field _translationHop in AiFaqHandler.MaybeTranslateAsync

- **Dosya:** `src/Invekto.Automation/Services/NodeHandlers/AiFaqHandler.cs` (325)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** _translationHop is declared as TranslationHopClient? (nullable). Inside MaybeTranslateAsync (a private method), it is accessed with ! suppressor: _translationHop!.TranslateAsync(...). The outer guard in MatchAndRoute at line 194 ensures the method is only called when _translationHop != null, but the private method itself offers no such guarantee. If MaybeTranslateAsync is ever called by a refactored code path that forgets the null-check, it silently dereferences null. Per repo hard-fail rules, null-forgiving operators are forbidden — explicit null guards or pattern-binding are required.
- **Kanıt:** `var translated = await _translationHop!.TranslateAsync(ctx.TenantId, answer, targetLocale, ctx.RequestId, ct);`
- **Önerilen fix:** Add an explicit guard at the top of MaybeTranslateAsync: if (_translationHop is not { } hop) return null; — then call hop.TranslateAsync(...). Remove the ! suppressor. This keeps the existing behavior (returns null on null client) and is safe to call from any path.

#### Automation-15 · [MEDIUM] Null-forgiving operator on nullable int rotationIndex in AiFaqHandler

- **Dosya:** `src/Invekto.Automation/Services/NodeHandlers/AiFaqHandler.cs` (208)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving
- **Sorun:** rotationIndex is declared as int? and is accessed with rotationIndex!.Value inside an if block guarded only by rotationCount.HasValue. The invariant that rotationIndex is set whenever rotationCount is set is maintained by the current code (they are assigned together in lines 183-184), but the compiler cannot prove this. Per repo hard-fail rules, null-forgiving operators are forbidden regardless of whether the surrounding logic makes null unreachable.
- **Kanıt:** `variables["faq_variant_index"] = rotationIndex!.Value.ToString(); — inside if (rotationCount.HasValue && rotationCount.Value > 0)`
- **Önerilen fix:** Replace the four separate nullable int fields with the existing RotationPick? result variable directly. Keep rotationResult in scope after the if block (move its declaration out of the inner if), then write: if (rotationResult.HasValue) { variables["faq_variant_index"] = rotationResult.Value.Index.ToString(); ... }. Removes the correlation problem and eliminates the ! operator.

#### Automation-16 · [MEDIUM] Null-forgiving operator in AiIntentHandler.ParseIntents LINQ chain

- **Dosya:** `src/Invekto.Automation/Services/NodeHandlers/AiIntentHandler.cs` (506)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** .Select(e => e.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!) — the second Select uses ! to cast string? to string after the Where filter. While the Where logically excludes null, the compiler still types the result as IEnumerable<string?> before the final Select, requiring the suppressor. Per repo hard-fail rules, null-forgiving is forbidden.
- **Kanıt:** `.Select(s => s!).ToArray() — after Where(s => !string.IsNullOrWhiteSpace(s))`
- **Önerilen fix:** Replace the chain ending with .OfType<string>() before .ToArray(), which changes the type to IEnumerable<string> without a suppressor: .Select(e => e.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)).OfType<string>().ToArray()

#### Automation-17 · [MEDIUM] PATCH method silently falls through to GET in ApiCallHandler.ParseMethod

- **Dosya:** `src/Invekto.Automation/Services/NodeHandlers/ApiCallHandler.cs` (301-307)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving
- **Sorun:** ParseMethod handles POST, PUT, DELETE; anything else (including PATCH, OPTIONS, HEAD) defaults to GET. Additionally, the body-attachment guard at line 120 only covers POST and PUT. If a flow operator configures method=PATCH, the handler silently sends a GET with no body instead of a PATCH, producing a wrong server-side action with no error surfaced to the flow or logs.
- **Kanıt:** `private static HttpMethod ParseMethod(string method) => method switch { "POST" => HttpMethod.Post, "PUT" => HttpMethod.Put, "DELETE" => HttpMethod.Delete, _ => HttpMethod.Get }; — and: if (method is "POST" or "PUT" && !string.IsNullOrEmpty(body))`
- **Önerilen fix:** Add PATCH to ParseMethod: "PATCH" => HttpMethod.Patch. Extend the body-attachment guard to include PATCH: if (method is "POST" or "PUT" or "PATCH" && ...). Optionally add an explicit rejection of unrecognised methods with an error NodeResult instead of silently falling through to GET.

#### Automation-18 · [MEDIUM] Bare catch in GetPriorRiskCountAsync swallows all exceptions without error code

- **Dosya:** `src/Invekto.Automation/Services/MarketingRescueClient.cs` (95-98)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** The catch block at line 95 is a bare `catch { return 0; }` — no type, no INV-XXX error code, no log. This is an explicit hard-fail pattern in this repo (broad catch is forbidden even with a `when` filter; requires typed catch + error code mapping). TaskCanceledException from app shutdown, NullReferenceException from a bug in the JSON deserializer path, and HttpRequestException are all silently swallowed and return 0, making the risk history score always zero when any infra hiccup occurs without any observability.
- **Kanıt:** `catch\n        {\n            return 0;\n        }`
- **Önerilen fix:** Replace the bare catch with typed catches matching the pattern used in the same file's other methods: `catch (HttpRequestException ex) { _logger.SystemWarn($"[{ErrorCodes.AutomationRescueMarketingFailed}] Prior risk count fetch failed for tenant {tenantId}: {ex.Message}"); return 0; } catch (OperationCanceledException) { return 0; } catch (JsonException ex) { _logger.SystemWarn($"[{ErrorCodes.AutomationRescueMarketingFailed}] Prior risk count parse error for tenant {tenantId}: {ex.Message}"); return 0; }`

#### Automation-19 · [LOW] Broad catch(Exception) inside LINQ projection in GET /flows/{tenantId} — hard-fail pattern

- **Dosya:** `src/Invekto.Automation/Program.cs` (662-667)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** Inside the per-flow LINQ `.Select()` projection, after a `catch (JsonException)` a second `catch (Exception ex)` catches everything else including NpgsqlException, NullReferenceException, etc. No INV-XXX code. If `validator.CalculateHealthScore` throws an unexpected type (e.g., `InvalidOperationException` for a graph build failure), it is silently degraded to healthScore=0 with a generic log and no error code.
- **Kanıt:** `catch (Exception ex) { jsonLogger.StepWarn($"Health score failed for flow {f.FlowId}: {ex.Message}", "-"); healthScore = 0; }`
- **Önerilen fix:** Add `catch (InvalidOperationException ioEx)` and `catch (ArgumentException argEx)` typed catches (matching the PUT handler's pattern on lines 877-884) before removing the broad catch. Each should log with `ErrorCodes.AutomationFlowValidationFailed`.

#### Automation-20 · [LOW] Null-forgiving operator (!) on flowConfig — hard-fail pattern

- **Dosya:** `src/Invekto.Automation/Program.cs` (850, 865, 891)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** In PUT /flows/{tenantId}/{flowId}, `flowConfig` is typed `string?` (line 830). After an early-return guard at line 831-832 (`if (string.IsNullOrEmpty(flowConfig)) return ...`), three subsequent uses suppress nullability with `flowConfig!` (lines 850, 865, 891). Per repo hard-fail rules, the null-forgiving operator is forbidden; the pattern-bind or explicit non-nullable reassignment must be used instead. While logically safe here, the `!` hides the original type and would fail if the guard were removed or moved.
- **Kanıt:** `newVersion = await repo.CreateFlowVersionAsync(tenantId, flowId, flowConfig!, "user"); ... var health = validator.CalculateHealthScore(flowConfig!); ... using var cfgDoc = JsonDocument.Parse(flowConfig!);`
- **Önerilen fix:** After the guard at line 832, reassign: `var validatedFlowConfig = flowConfig;` declared as `string validatedFlowConfig = flowConfig;` (compiler infers non-null after IsNullOrEmpty). Or use: `if (string.IsNullOrEmpty(flowConfig)) return ...; string nonNullConfig = flowConfig;` then replace all `flowConfig!` with `nonNullConfig`.

#### Automation-21 · [LOW] External webhook endpoint has no authentication — documented but flagged for visibility

- **Dosya:** `src/Invekto.Automation/Program.cs` (1770-1773)
- **Boyut:** security · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** POST /api/v1/webhooks/{tenantId}/{flowId} is explicitly outside the JWT middleware prefix and carries a comment 'No auth — security through URL obscurity (Q's decision)'. Any caller who discovers the URL can trigger flow executions for any active webhook-trigger flow for any tenant, creating sessions and firing outbound messages without authentication. The tenantId in the URL is attacker-controlled. The only guard is that the flow must be active and of type webhook_trigger.
- **Kanıt:** `// No auth — security through URL obscurity (Q's decision) ⏎ app.MapPost("/api/v1/webhooks/{tenantId:int}/{flowId:int}", ...`
- **Önerilen fix:** If intentional, at minimum add a secret-token check: read a `X-Webhook-Secret` header and compare to a per-flow or per-tenant secret stored in DB. This converts URL-obscurity to HMAC-style authentication with negligible overhead. If a full HMAC is too heavy, even a fixed shared-secret per tenant stored in tenant settings is significantly safer.

#### Automation-22 · [LOW] CreateSessionAsync: expire-then-insert is not atomic — two statements, no transaction

- **Dosya:** `src/Invekto.Automation/Data/AutomationRepository.cs` (760-785)
- **Boyut:** correctness · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** CreateSessionAsync first expires existing active sessions (UPDATE ... SET status='expired') and then inserts a new session (INSERT) as two separate statements on the same connection but without a transaction. If the INSERT fails (constraint violation, network drop) after the UPDATE succeeded, the old session has been expired but no new session exists — the chat is left with no active session and the user's next message will receive no response.
- **Kanıt:** `await expireCmd.ExecuteNonQueryAsync(ct); // UPDATE ⏎ // no transaction ⏎ await cmd.ExecuteScalarAsync(ct); // INSERT — can fail here`
- **Önerilen fix:** Wrap both statements in a single BeginTransactionAsync/CommitAsync block so that expire and create are atomic. On rollback the old active session is preserved.

#### Automation-23 · [LOW] UpsertLeadPreferredLocaleAsync: PostgresException(23505) catch is dead code for ON CONFLICT target

- **Dosya:** `src/Invekto.Automation/Data/AutomationRepository.cs` (1001-1012)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving
- **Sorun:** The code catches PostgresException with SqlState 23505 (unique violation) on an INSERT ... ON CONFLICT DO UPDATE statement. An ON CONFLICT DO UPDATE never raises a unique violation for its own conflict target — it handles it. A 23505 could only arise from a different unique constraint on the same table (e.g. a partial index). The comment 'Unique violation: expected when no unique constraint covers (tenant_id, phone)' is self-contradictory: if there is no unique constraint, ON CONFLICT has no target (42P10), not 23505. This catch block is likely dead code and misleading.
- **Kanıt:** `catch (PostgresException ex) when (ex.SqlState == "23505") { // Unique violation: expected when no unique constraint covers (tenant_id, phone). }`
- **Önerilen fix:** Remove the 23505 catch block (it cannot be triggered by this statement pattern) or document precisely which other unique constraint on leads could trigger it. If there truly is no unique index on (tenant_id, phone) then only 42P10 applies, which is already caught on line 1007.

#### Automation-24 · [LOW] Unsynchronized concurrent mutation of SimulationSession.State in StepAsync

- **Dosya:** `src/Invekto.Automation/Services/SimulationEngine.cs` (146-219)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** ConcurrentDictionary protects the _sessions collection itself (lookup/removal is thread-safe), but the SimulationSession objects stored as values are plain mutable objects. Two concurrent HTTP requests with the same sessionId both pass the TryGetValue + expiry + status checks and then both call session.State.Variables["__last_input"] = userMessage (line 172) and await _engine.ExecuteAsync(session.Graph, session.State, ...) (line 175) on the same state object simultaneously. SessionStateV2.Variables is Dictionary<string,string> (not concurrent), ExecutionPath is List<string> (not concurrent), and PathSnapshotCount (line 266 in LogUpdateAsync) is a plain int — all mutated without any lock. Double-step corruption of the same simulation session is the result.
- **Kanıt:** `session.State.Variables["__last_input"] = userMessage; // line 172 — no lock\nvar result = await _engine.ExecuteAsync(session.Graph, session.State, ct, ...); // line 175 — mutates state in the engine loop\nsession.PathSnapshotCount = session.State.ExecutionPath.Count; // line 266 — unsynchronized in…`
- **Önerilen fix:** Add a SemaphoreSlim(1,1) per SimulationSession (stored on the session object). In StepAsync, await semaphore.WaitAsync(ct) before reading the session expiry/status and release in a finally block after the engine step completes and LastActivityAt/ExpiresAt are updated. This is the standard pattern for per-entity serialization without global locking.

#### Automation-25 · [LOW] DFS cycle detection marks only back-edge endpoints — intermediate cycle nodes silently omitted from validator warnings

- **Dosya:** `src/Invekto.Automation/Services/FlowValidator.cs` (359-381)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving
- **Sorun:** DfsCycleCheck adds only two nodes per detected back-edge: the target already on the DFS stack (the cycle entry point) and the current node (the back-edge source). For a 3-node cycle A→B→C→A, when the C→A back-edge is found, A and C are added to cycleNodes but B is never added. The resulting warning list tells the user that A and C are in a cycle without mentioning B, making the validator feedback incomplete and potentially misleading. This does not affect runtime safety (FlowEngineV2 has its own MaxLoopCount/MaxChainDepth guards), but reduces diagnostic value for flow designers.
- **Kanıt:** `else if (inStack.Contains(edge.Target)) { cycleNodes.Add(edge.Target); cycleNodes.Add(nodeId); } // lines 373-377 — only two nodes added, not all nodes currently in inStack between edge.Target and nodeId`
- **Önerilen fix:** When a back-edge is detected, walk the inStack to collect all nodes on the path from edge.Target to nodeId and add all of them to cycleNodes. Since inStack is a HashSet (unordered), switch it to a List<string> used as a stack (push on enter, pop on exit) and slice from the index of edge.Target to the end to get all cycle members.

#### Automation-26 · [LOW] Duplicate XML summary block on TryDecodeChunkPayload in MessageTextHandler

- **Dosya:** `src/Invekto.Automation/Services/NodeHandlers/MessageTextHandler.cs` (336-345)
- **Boyut:** duplication · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** TryDecodeChunkPayload has two consecutive /// <summary> XML doc blocks. The first (lines 336-339) is a stale copy from a previous signature that said 'Returns null when the input is not a chunked payload or the JSON is malformed (caller dispatches as a single legacy message).' The second (lines 340-345) is the current accurate description with the callback behaviour. Standard XML doc parsers take only the last summary; docfx/IDE tools may warn or render both, creating confusing documentation.
- **Kanıt:** `Lines 335-345: closing </summary> of the first block immediately followed by a second /// <summary> opening block before the method signature.`
- **Önerilen fix:** Delete the first (stale) /// <summary>...</summary> block at lines 336-339, keeping only the current accurate description.

#### Automation-27 · [LOW] TranslationHopClient uses Headers.Add for Authorization instead of typed AuthenticationHeaderValue

- **Dosya:** `src/Invekto.Automation/Services/TranslationHopClient.cs` (58)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** Line 58 sets the Authorization header via `request.Headers.Add("Authorization", $"Bearer {token}")`. All other clients in this unit use the typed `request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token)` form. The `Headers.Add` path bypasses .NET's header validation and can silently add a malformed header value if the JWT contains unexpected characters, whereas `AuthenticationHeaderValue` validates the scheme and parameter at construction time and throws `FormatException` early. This is a consistency and latent-bug risk; the token is not cross-tenant (per-request mint) so there is no security issue here.
- **Kanıt:** `request.Headers.Add("Authorization", $"Bearer {_jwtGenerator.GenerateServiceToken(tenantId)}");`
- **Önerilen fix:** Replace with: `request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtGenerator.GenerateServiceToken(tenantId));`

### Shared (22)

#### Shared-1 · [CRITICAL] INV-BE-090..094: five code values each assigned to TWO different constants (real collision)

- **Dosya:** `src/Invekto.Shared/Constants/ErrorCodes.cs` (67-71, 689-694)
- **Boyut:** correctness · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** Five string literals (INV-BE-090 through INV-BE-094) are each assigned twice: once in the 'BackendTranslationWarmup*' group (lines 67-71) and again in the 'BackendTranslation*' group (lines 689-694). Both sets exist simultaneously. Any code that switches on error code strings (logs, dashboards, alerting) cannot distinguish the two error classes. Additionally, errors.md contains two separate YAML blocks that both define INV-BE-090..094 with different descriptions (Chat Translation API vs. Warmup ops endpoint), compounding the confusion.
- **Kanıt:** `Line 67: `BackendTranslationWarmupInvalidPayload = "INV-BE-090"` and line 689: `BackendTranslationFailed = "INV-BE-090"`. Same value for two constants with entirely different semantics. PowerShell set-difference confirmed both exist.`
- **Önerilen fix:** The BackendTranslationWarmup* group (lines 67-71, added for HFM-2 Warmup ops endpoint) was allocated the same range as the general Chat Translation group (lines 689-694). The Warmup constants must be renumbered to a fresh range (e.g., INV-BE-090..094 stay as the general translation codes, and the Warmup codes get INV-BE-095 is already taken, so allocate INV-BE-134..138 or whatever the next free block is). Remove the duplicate constant definitions at lines 67-71 and update all Warmup callers to the new codes. Mirror in errors.md.

#### Shared-2 · [CRITICAL] INV-BE-110: single value assigned to both LeadIntakeInternalAuthInvalid and FieldMappingDbUnavailable

- **Dosya:** `src/Invekto.Shared/Constants/ErrorCodes.cs` (86, 701)
- **Boyut:** correctness · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** INV-BE-110 is assigned to `LeadIntakeInternalAuthInvalid` (line 86, X-Internal-Service-Token auth failure on /api/internal/leads/intake/wa-direct) and also to `FieldMappingDbUnavailable` (line 701, tenant_settings.field_mapping DB failure). Two operationally distinct error conditions share one code. Monitoring, alerting and client error-handling that branches on the code string cannot tell a service-to-service auth failure from a DB outage.
- **Kanıt:** `Line 86: `LeadIntakeInternalAuthInvalid = "INV-BE-110"` and line 701: `FieldMappingDbUnavailable = "INV-BE-110"`. errors.md also has two separate entries for INV-BE-110 with different descriptions, confirming the divergence.`
- **Önerilen fix:** FieldMappingDbUnavailable (TFM-specific DB error, added later) was allocated INV-BE-110 without noticing the existing LIW assignment. Renumber FieldMappingDbUnavailable to the next available BE code (check arch/errors.md for the current high-water mark; INV-BE-134 onward is free). Update the single caller site in TFM code and mirror in errors.md. Remove the duplicate entry in errors.md.

#### Shared-3 · [HIGH] HttpInmaDynamicFieldsClient uses req.Headers.Add instead of TryAddWithoutValidation — throws FormatException on non-ASCII secret chars

- **Dosya:** `src/Invekto.Shared/Contracts/Inma/HttpInmaDynamicFieldsClient.cs` (53)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** Line 53 calls `req.Headers.Add(SecretKeyHeader, secretKey)` which runs .NET header format validation (RFC 7230). If the tenant's cxapi secret contains characters that fail that validation (semicolons, commas, or other token-separator chars that appear in some API key formats), Add throws a FormatException that propagates uncaught out of `GetFieldsAsync`. The surrounding catch chain only handles `TaskCanceledException` and `HttpRequestException`, so FormatException escapes to the cache caller, crashes the request, and the tenantId/error detail are never logged. Every other cxapi client in this repo uses `TryAddWithoutValidation` specifically to avoid this class of failure.
- **Kanıt:** `req.Headers.Add(SecretKeyHeader, secretKey); // line 53 — unlike WapCrmSendClient/WapCrmTemplateClient/WapCrmFeatureGroupCatalogClient/WapCrmWebhookSettingsClient which all use req.Headers.TryAddWithoutValidation(SecretKeyHeader, secretKey)`
- **Önerilen fix:** Replace `req.Headers.Add(SecretKeyHeader, secretKey);` with `if (!req.Headers.TryAddWithoutValidation(SecretKeyHeader, secretKey)) throw new InmaDynamicFieldsFetchException(tenantId, "Failed to attach WapCRM secret header");`. Also add a `ContainsControlChars` pre-check (already present verbatim in WapCrmTemplateClient and WapCrmFeatureGroupCatalogClient) to give a clean ArgumentException before the HTTP call for genuinely malformed secrets.

#### Shared-4 · [MEDIUM] inma_dynamicfields HttpClient registered without AllowAutoRedirect=false — cxapi 301/302 rate-limit silently followed as redirect

- **Dosya:** `src/Invekto.Backend/Program.cs` (487-500)
- **Boyut:** correctness · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** The named HttpClient `inma_dynamicfields` is registered with only `client.Timeout = TimeSpan.FromSeconds(5)` and no `ConfigurePrimaryHttpMessageHandler`. The default HttpClientHandler has `AllowAutoRedirect=true`. When cxapi returns HTTP 301 or 302 (its rate-limit signal), the client silently follows the redirect to whatever Location header is present — potentially an unintended endpoint — rather than surfacing a rate-limit failure. `HttpInmaDynamicFieldsClient` does not check the HTTP status code before attempting body parse, so the redirect response body (often an HTML error page from the redirect target) is deserialized as InmaDynamicFieldsResponse, fails parse, and throws InmaDynamicFieldsFetchException with 'malformed JSON' — masking the real rate-limit cause. Every other cxapi client registration (WapCrmSendClient, WapCrmTemplateClient, WapCrmFeatureGroupCatalogClient, WapCrmWebhookSettingsClient) explicitly sets AllowAutoRedirect=false.
- **Kanıt:** `builder.Services.AddHttpClient("inma_dynamicfields", client => { client.Timeout = TimeSpan.FromSeconds(5); }); // lines 487-490 — no ConfigurePrimaryHttpMessageHandler, no AllowAutoRedirect=false`
- **Önerilen fix:** Add `.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })` to the `AddHttpClient("inma_dynamicfields", ...)` call. Then add a redirect-status check inside `HttpInmaDynamicFieldsClient.GetFieldsAsync` after `await _httpClient.SendAsync(req, ct)` — before reading the body — matching the pattern in WapCrmFeatureGroupCatalogClient lines 85-88: `if (http is 301 or 302) { _logger.SystemWarn(...); throw new InmaDynamicFieldsFetchException(tenantId, "INMA dynamic-fields rate-limited", httpStatusCode: http); }`.

#### Shared-5 · [MEDIUM] Invalidate/inflight race: in-flight DB fetch writes stale data to cache after Invalidate()

- **Dosya:** `src/Invekto.Shared/Contracts/Campaigns/DbTenantCampaignResolver.cs` (71-75, 220-246)
- **Boyut:** correctness · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR · _(orijinal sev: high → düzeltildi)_
- **Sorun:** When Invalidate(tenantId) is called (e.g. after a PUT upsert) while a FetchAndCacheAsync task is already mid-flight, the following race occurs: (1) Invalidate removes the cache entry and removes the inflight task reference (lines 73-74). (2) The already-in-flight FetchAndCacheAsync completes its LoadFromDbAsync call, returning data fetched from DB BEFORE the PUT committed. (3) FetchAndCacheAsync calls _cache.Set at line 226, overwriting the just-invalidated cache with pre-PUT stale data. (4) This stale entry then lives in cache for up to 5 minutes, so the PUT's new campaign config is invisible until TTL expiry. The window guard and substitution silently serve old config. The doc comment says 'Backend PUT calls Invalidate after a successful upsert so the next resolver call reads fresh state' — this guarantee is violated when a concurrent read is in flight at PUT time.
- **Kanıt:** `Line 73-74: _cache.Remove(CacheKey(tenantId)); _inflight.TryRemove(tenantId, out _);  — Line 226: _cache.Set(CacheKey(tenantId), loaded, CacheTtl); — The _cache.Set at line 226 executes after Invalidate has already cleared the key; there is no version/generation guard preventing a stale overwrite.`
- **Önerilen fix:** Add a per-tenant generation counter (e.g. ConcurrentDictionary<int, long> _generation) incremented by Invalidate, snapshot before the fetch starts, and guard _cache.Set with a compare-and-swap: only call _cache.Set if the current generation equals the snapshot. In FetchAndCacheAsync: capture generation = _generation.GetOrAdd(tenantId, 0) before LoadFromDbAsync; after load, only call _cache.Set if _generation[tenantId] still equals that snapshot. In Invalidate: call _generation.AddOrUpdate(tenantId, 1, (_, v) => v + 1) before removing inflight.

#### Shared-6 · [MEDIUM] Nullable boolean filter logic inverts service and search filters — passes null-field entries incorrectly

- **Dosya:** `src/Invekto.Shared/Logging/Reader/LogReader.cs` (123-131)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving
- **Sorun:** In `QueryLogsAsync`, the service filter and search filter both use the pattern `!e.Field?.Contains(...) == true`. Because `!` binds tighter than `==`, this evaluates as `(!e.Field?.Contains(...)) == true`. When `e.Field` is `null`, the null-conditional returns `null` (bool?), `!null` is also `null` (bool?), and `null == true` is `false`, so the `return false` branch is never taken — entries with a null Service, null Message, and null RequestId pass through the filter even when a service or search filter is active. The correct intent is to reject entries that do not match. The search filter at lines 128-131 has the same structure for both Message and RequestId, compounded with an AND that means both must be null/missing for the entry to incorrectly pass.
- **Kanıt:** `Line 123-125: `if (!string.IsNullOrEmpty(options.Service) && !e.Service?.Contains(options.Service, StringComparison.OrdinalIgnoreCase) == true) return false;` — null Service bypasses this return. Lines 128-131: same structure for Message and RequestId.`
- **Önerilen fix:** Rewrite using explicit null-guard: `if (!string.IsNullOrEmpty(options.Service) && e.Service?.Contains(options.Service, StringComparison.OrdinalIgnoreCase) != true) return false;` — `!= true` correctly handles both null (returns false → rejected) and non-matching (false → rejected). Apply the same transformation to the search filter: `if (!string.IsNullOrEmpty(options.Search) && e.Message?.Contains(options.Search, StringComparison.OrdinalIgnoreCase) != true && e.RequestId?.Contains(options.Search, StringComparison.OrdinalIgnoreCase) != true) return false;`

#### Shared-7 · [MEDIUM] Null-forgiving operator `!` on `_writer` suppresses real post-dispose NullReferenceException

- **Dosya:** `src/Invekto.Shared/Logging/JsonLinesLogger.cs` (254)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** `Dispose()` at lines 261-264 acquires the lock, calls `_writer?.Dispose()`, then sets `_writer = null`. If `WriteLine` is called after `Dispose()` on the same calendar day, the `_currentFileName != fileName` check at line 246 evaluates to `false` (same filename), so the `_writer` reassignment branch is skipped, and `_writer!.WriteLine(line)` at line 254 throws `NullReferenceException`. The null-forgiving `!` operator hides this from static analysis — it is the exact pattern the repo's hard-fail list forbids. The exception will propagate uncaught to the caller, which is a log write site and usually has no handler.
- **Kanıt:** `Line 254: `_writer!.WriteLine(line);` inside `lock (_lock)`. Line 263: `_writer = null;` in `Dispose()`, also inside `lock (_lock)`.`
- **Önerilen fix:** Replace the null-forgiving operator with an explicit disposed guard: `if (_writer is null) return; // disposed — drop the log line silently` placed immediately before line 254, removing the `!`. Alternatively use `_writer?.WriteLine(line)` to silently drop post-dispose writes, which is acceptable for a logger.

#### Shared-8 · [MEDIUM] Path traversal in GetLogContextAsync: unsanitized fileName from HTTP query parameter

- **Dosya:** `src/Invekto.Shared/Logging/Reader/LogReader.cs` (263-314)
- **Boyut:** security · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The `fileName` parameter is passed verbatim from the HTTP query string (Program.cs line 1611: `await logReader.GetLogContextAsync(file, line.Value, range ?? 10)`) and combined with each log directory via `Path.Combine(dir, fileName)` at line 269 with no sanitization. A value such as `../../appsettings.Production.json` resolves to a file outside the log directory. The file is opened with `FileShare.ReadWrite` (line 541) and its lines are attempted as JSON; non-JSON content is silently skipped, but any line that happens to parse as a valid LogEntryDto (e.g. a crafted file containing `{"message":"..."}`) is returned to the caller. The endpoint is behind `ValidateOpsAuth` (Basic/Bearer), so this requires authenticated access — but ops credentials are shared across the ops-admin surface and an attacker who obtains them can read arbitrary files from the server process's working directory.
- **Kanıt:** `LogReader.cs line 269: `var filePath = Path.Combine(dir, fileName);` where `fileName` comes directly from the caller with no `Path.GetFileName` or `..` rejection. Backend Program.cs line 1611 passes the raw HTTP query-string `file` parameter.`
- **Önerilen fix:** In `GetLogContextAsync`, sanitize the input to just the filename component before combining: `var safeFileName = Path.GetFileName(fileName); if (string.IsNullOrEmpty(safeFileName)) return result; var filePath = Path.Combine(dir, safeFileName);` — `Path.GetFileName` strips any directory component, making traversal impossible. Optionally also enforce the `.jsonl` extension: `if (!safeFileName.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)) return result;`

#### Shared-9 · [MEDIUM] WapCrmFeatureGroupCatalogCache is an almost-verbatim copy of InmaDynamicFieldsCache — 90+ lines duplicated

- **Dosya:** `src/Invekto.Shared/Services/WapCrmFeatureGroupCatalogCache.cs` (25-95)
- **Boyut:** duplication · **Fix-risk:** medium · behavior-preserving
- **Sorun:** WapCrmFeatureGroupCatalogCache and InmaDynamicFieldsCache implement the exact same single-flight IMemoryCache pattern: GetOrFetchAsync / FetchAndCacheAsync / AwaitWithCallerCancellation / Invalidate / CacheKey — differing only in the generic payload type (IReadOnlyList<CustomerFeatureGroupView> vs IReadOnlyList<InmaDynamicField>), the cache key prefix string, and the injected client type. All methods, cancellation-isolation logic, and TTL constant (1h) are copied line-for-line. Every future correctness improvement (e.g. the stampede fix in 2026-04-22) must be applied twice.
- **Kanıt:** `WapCrmFeatureGroupCatalogCache.cs lines 60-65 'private static async Task<...> AwaitWithCallerCancellation' is identical to InmaDynamicFieldsCache.cs lines 76-82. FetchAndCacheAsync, Invalidate, and CacheKey patterns match character-for-character except for type parameters and prefix string.`
- **Önerilen fix:** Extract a generic SingleFlightMemoryCache<TClient, TResult> base class or helper that accepts a Func<int, string, CancellationToken, Task<IReadOnlyList<TResult>>> fetch delegate, a cache-key prefix, and TTL. Both caches become thin wrappers delegating to this shared core. This is a refactor only — behavior is fully preserved and both classes continue to expose their current public APIs.

#### Shared-10 · [LOW] IsRedirectRateLimit / IsEnvelopeRateLimit / ParseRetryAfter / ContainsControlChars duplicated across four client classes

- **Dosya:** `src/Invekto.Shared/Contracts/Inma/WapCrmSendClient.cs` (548-550 (WapCrmSendClient), 166-168 (WapCrmTemplateClient), 195 (WapCrmWebhookSettingsClient), 170-178 (WapCrmTemplateClient), 145-153 (WapCrmFeatureGroupCatalogClient))
- **Boyut:** duplication · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** `IsRedirectRateLimit` (httpStatus is 301 or 302) is a private static in WapCrmSendClient, WapCrmTemplateClient, and WapCrmWebhookSettingsClient. `IsEnvelopeRateLimit` is duplicated in WapCrmSendClient and WapCrmTemplateClient. `ParseRetryAfter` is duplicated in WapCrmSendClient and WapCrmTemplateClient. `ContainsControlChars` is duplicated in WapCrmTemplateClient and WapCrmFeatureGroupCatalogClient. There is no single source of truth for the rate-limit codes (301/302 as application-layer signals). If cxapi ever adds a new rate-limit code, all copies must be found and updated independently.
- **Kanıt:** `Grep for `IsRedirectRateLimit|IsEnvelopeRateLimit|ParseRetryAfter|ContainsControlChars` across src/Invekto.Shared/Contracts/Inma shows 4-6 copies of identical implementations across separate sealed classes.`
- **Önerilen fix:** Extract to an internal static helper class `CxapiClientHelper` in the same namespace (e.g. `src/Invekto.Shared/Contracts/Inma/CxapiClientHelper.cs`): `internal static class CxapiClientHelper { internal static bool IsRedirectRateLimit(int http) => http is 301 or 302; internal static bool IsEnvelopeRateLimit(string? code) => code is "301" or "302"; internal static TimeSpan? ParseRetryAfter(HttpResponseMessage r) {...}; internal static bool ContainsControlChars(string s) {...} }`. All four client classes reference the shared helper. This is fully behavior-preserving.

#### Shared-11 · [LOW] WapCrmApiEnvelope in WapCrmInstance.cs is a non-generic duplicate of WapCrmApiResponse<List<WapCrmRawInstance>>

- **Dosya:** `src/Invekto.Shared/Contracts/Inma/Dtos/WapCrmInstance.cs` (23-29)
- **Boyut:** duplication · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** `WapCrmApiEnvelope` (Status bool, Message string?, Data List<WapCrmRawInstance>?) is a hand-rolled, non-generic envelope with identical semantics to the already-present `WapCrmApiResponse<T>` (Status, Message, Data, StatusCode, RequestId). The generic type is used by all cxapi clients for JSON deserialization. `WapCrmApiEnvelope` is only consumed in one place: Backend Program.cs line 3950 for the `/api/Instances` admin endpoint. It lacks `statusCode` and `requestID` fields that `WapCrmApiResponse<T>` carries. If the cxapi envelope contract changes, only `WapCrmApiResponse<T>` would be updated.
- **Kanıt:** `public sealed class WapCrmApiEnvelope { public bool Status { get; set; } public string? Message { get; set; } public List<WapCrmRawInstance>? Data { get; set; } } // WapCrmInstance.cs lines 24-29 — vs WapCrmApiResponse<T> in WapCrmMessage.cs which carries the same three fields plus StatusCode and Re…`
- **Önerilen fix:** Replace `WapCrmApiEnvelope` with `WapCrmApiResponse<List<WapCrmRawInstance>>` at the usage site in Backend Program.cs. Change `JsonSerializer.Deserialize<WapCrmApiEnvelope>(json, ...)` to `JsonSerializer.Deserialize<WapCrmApiResponse<List<WapCrmRawInstance>>>(json, ...)` and update the null/data check to `apiResp?.Data`. Delete the `WapCrmApiEnvelope` class.

#### Shared-12 · [LOW] Non-Npgsql / non-JsonException escapes FetchAndCacheAsync and surfaces raw to all joined callers

- **Dosya:** `src/Invekto.Shared/Contracts/Campaigns/DbTenantCampaignResolver.cs` (220-247)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** FetchAndCacheAsync only catches NpgsqlException (line 229) and JsonException (line 236). The doc comment in both the implementation and ITenantCampaignResolver says 'any DB / JSON failure returns CampaignConfig.Empty + WARN log under INV-BE-121/118'. However, any exception that is not NpgsqlException or JsonException — e.g. ObjectDisposedException from a disposed NpgsqlConnection, InvalidOperationException from Npgsql's connection-pool state, SocketException wrapped in a non-Npgsql exception, or TaskCanceledException from an internal timeout — will propagate as a faulted Task. Because all concurrent callers share the same Task via the single-flight pattern (GetOrAdd), they all receive the raw unhandled exception. The callers (GetAsync, IsWithinWindowAsync, RenderPlaceholderAsync) have no try-catch guard, so the exception propagates to AutomationOrchestrator's SendCallbackAsync and CampaignTemplateApplier.ApplyAsync, potentially crashing the outbound dispatch path contrary to the stated 'fail-soft' contract.
- **Kanıt:** `Lines 229-242: only NpgsqlException and JsonException are caught. Lines 65-68 (GetAsync), 77-95 (IsWithinWindowAsync), 98-135 (RenderPlaceholderAsync): no try-catch on GetOrFetchAsync. The ITenantCampaignResolver summary states 'Resolver fail must NOT crash the outbound path' — this contract is brok…`
- **Önerilen fix:** Add a final catch-all non-Exception (to avoid swallowing OOM/fatal) or a targeted catch for ObjectDisposedException / InvalidOperationException / SocketException in FetchAndCacheAsync, logging under INV-BE-121 and returning CachedTenantCampaign.EmptyFor(null). Alternatively, add a try-catch in GetOrFetchAsync so all callers are uniformly protected. Use typed catches: catch (ObjectDisposedException ex) and catch (InvalidOperationException ex) after the JsonException block.

#### Shared-13 · [LOW] locale parameter null-guard is dead code (parameter is non-nullable string)

- **Dosya:** `src/Invekto.Shared/Contracts/Campaigns/DbTenantCampaignResolver.cs` (170, 177)
- **Boyut:** simplification · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** RenderCitiesHuman at line 170 declares `string locale` (non-nullable). The condition at line 177 is `locale != null && locale.StartsWith(...)`. Since `locale` is a non-nullable reference type parameter, the `locale != null` check is always true at compile time (unless nullable annotations are disabled project-wide). The check is dead code: removing it has no behavioral effect but eliminates misleading defensive noise that suggests `locale` could be null.
- **Kanıt:** `Line 170: `private static string RenderCitiesHuman(CampaignEntry campaign, string locale)` — non-nullable. Line 177: `var conjunction = locale != null && locale.StartsWith("tr", ...)` — null guard on non-nullable.`
- **Önerilen fix:** Replace `locale != null && locale.StartsWith("tr", StringComparison.OrdinalIgnoreCase)` with just `locale.StartsWith("tr", StringComparison.OrdinalIgnoreCase)`.

#### Shared-14 · [LOW] Bare catch blocks swallow all exceptions (repo hard-fail)

- **Dosya:** `src/Invekto.Shared/Logging/Reader/LogReader.cs` (298,307,374,400,463,469,516,522)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** Every exception-swallowing site in LogReader.cs uses an untyped `catch { }` block. The repo rule forbids bare catch(Exception) and bare catch alike — each must be a typed catch (e.g. `catch (IOException)`, `catch (JsonException)`) with no suppressed "locked file" rationale covering every possible failure mode. Lines 307 and 469/522 are the outer file-open catch blocks; lines 298/463/516 are the inner JSON-parse catch blocks. A `StackOverflowException` or `OutOfMemoryException` would be silently absorbed at the outer level. More practically, a programming error (e.g. wrong predicate throwing InvalidOperationException) is swallowed and the caller receives an empty list with no signal.
- **Kanıt:** `Line 298: `catch { // Skip malformed lines }` (inner); Line 307: `catch { // File may be locked }` (outer); Lines 463, 469, 516, 522: same pattern in ReadEntriesWithIdFromFileAsync and ReadEntriesFromFileAsync.`
- **Önerilen fix:** Inner JSON-parse catch: `catch (JsonException) { /* skip malformed line */ }`. Outer file-open catch: `catch (IOException) { /* file locked or deleted */ }`. Do not catch Exception or use bare catch. For the outer block, optionally log via a fallback mechanism so silent file-open failures are observable.

#### Shared-15 · [LOW] INV-WA-001..027: entire WhatsApp Analytics namespace defined in code but has zero entries in errors.md

- **Dosya:** `src/Invekto.Shared/Constants/ErrorCodes.cs` (427-459)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** 27 constants across the INV-WA-* namespace (WA Analytics, Insight Engine, Nightly Batch, RI Cross-Service) are defined in ErrorCodes.cs. The WA service is declared in the errors.md YAML service header but the YAML `errors:` block contains zero `- code: INV-WA-*` entries. This violates the bi-directional sync requirement stated in errors.md ('all error messages must use codes from this file') and means the WA namespace is unreviewed in the registry.
- **Kanıt:** `PowerShell grep confirmed `INV-WA-` appears zero times in errors.md error entries. YAML services block has `WA: { name: WhatsAppAnalytics, ... }` but no entries follow in the registry section.`
- **Önerilen fix:** Add all 27 INV-WA-xxx entries to the `# ── WA ──` section in errors.md, following the pattern of other service blocks. Each entry needs code, description, and user_message. This is documentation-only; no code changes needed.

#### Shared-16 · [LOW] INV-LD-001..008: Lead Management namespace defined in code, entirely absent from errors.md

- **Dosya:** `src/Invekto.Shared/Constants/ErrorCodes.cs` (609-617)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** Eight constants for the INV-LD-* Lead Management namespace (GR-3.13) are defined in ErrorCodes.cs but neither the LD service code nor any entries appear in errors.md. Unlike INV-WA-*, there is not even a service header entry for LD. The service description in CLAUDE.md references GR-3.13 as an existing feature group, so this is a straightforward omission.
- **Kanıt:** `PowerShell set-difference: INV-LD-001 through INV-LD-008 all appear in CS, none appear in errors.md. `grep 'INV-LD-'` in errors.md returns zero matches. No `LD:` entry in the services YAML block.`
- **Önerilen fix:** Add `LD: { name: LeadManagement, description: 'GR-3.13: Lead pipeline hataları' }` to the services YAML block in errors.md, then add all 8 INV-LD-xxx entries to the registry. Documentation-only change.

#### Shared-17 · [LOW] INV-AA-010..012: AgentAI e-commerce codes defined in code but absent from errors.md

- **Dosya:** `src/Invekto.Shared/Constants/ErrorCodes.cs` (344-347)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** Three AgentAI PKT-6B1 codes (AgentAIOrderCardFetchFailed, AgentAIEscalationNoteFailed, AgentAIEcomReplyEnrichFailed) are defined in code starting at line 344 but have no entries in errors.md. The errors.md AA section ends at INV-AA-009.
- **Kanıt:** `Line 344: `AgentAIOrderCardFetchFailed = "INV-AA-010"`. PowerShell set-difference confirmed INV-AA-010, INV-AA-011, INV-AA-012 are in CS only.`
- **Önerilen fix:** Add INV-AA-010, INV-AA-011, INV-AA-012 entries after the existing INV-AA-009 entry in errors.md. Documentation-only.

#### Shared-18 · [LOW] INV-KN-033..035: Knowledge Intent CRUD codes defined in code but absent from errors.md

- **Dosya:** `src/Invekto.Shared/Constants/ErrorCodes.cs` (413-416)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** Three Knowledge Intent CRUD codes (KnowledgeIntentCreateFailed, KnowledgeIntentUpdateFailed, KnowledgeIntentDeleteFailed) are defined at lines 413-416 but are absent from errors.md. The errors.md KN section jumps from INV-KN-032 directly to INV-KN-036, skipping 033-035.
- **Kanıt:** `Line 413: `KnowledgeIntentCreateFailed = "INV-KN-033"`. PowerShell set-difference confirmed INV-KN-033, INV-KN-034, INV-KN-035 are in CS only, not in errors.md.`
- **Önerilen fix:** Add INV-KN-033, INV-KN-034, INV-KN-035 entries between INV-KN-032 and INV-KN-036 in errors.md. Documentation-only.

#### Shared-19 · [LOW] INV-OB-021 and INV-OB-022..023: codes defined in code but only INV-OB-021 is mentioned (as reserved comment) in errors.md

- **Dosya:** `src/Invekto.Shared/Constants/ErrorCodes.cs` (493-496)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** INV-OB-021 (OutboundEcomTriggerTemplateMissing), INV-OB-022 (OutboundClinicTriggerTemplateMissing), and INV-OB-023 (OutboundLeadFollowUpFailed) are defined in CS. errors.md has only a comment `# INV-OB-021..023 reserved in ErrorCodes.cs by PKT-6B1` with no actual registry entries for any of the three. INV-OB-022 and INV-OB-023 do not even appear as text in errors.md.
- **Kanıt:** `errors.md line 946: `# INV-OB-021..023 reserved in ErrorCodes.cs by PKT-6B1 (ecom/clinic triggers, lead follow-up).` - only a comment, no `- code:` entries. PowerShell confirmed INV-OB-022 and INV-OB-023 are entirely absent from errors.md text.`
- **Önerilen fix:** Replace the comment with actual registry entries for INV-OB-021, INV-OB-022, INV-OB-023. Documentation-only.

#### Shared-20 · [LOW] INV-JOB-006: documented in errors.md but no constant in ErrorCodes.cs

- **Dosya:** `arch/errors.md` (1551-1553)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** errors.md has an entry for INV-JOB-006 (orphan 'default' queue detected at startup), but ErrorCodes.cs jumps from INV-JOB-005 to INV-JOB-010 with no constant for 006. The startup-time probe that emits this code must use a raw string literal.
- **Kanıt:** `PowerShell set-difference confirmed INV-JOB-006 in errors.md only. ErrorCodes.cs line 762 jumps from `JobExecutionFailed = "INV-JOB-005"` to `DbBackupPgDumpFailed = "INV-JOB-010"`.`
- **Önerilen fix:** Add `public const string JobOrphanQueueDetected = "INV-JOB-006";` after line 761 in ErrorCodes.cs. Grep codebase for the raw string `"INV-JOB-006"` and replace with the constant.

#### Shared-21 · [LOW] AnalysisCriterion and PurchaseProbabilityCriterion share a duplicated Summary+Details+Empty pattern without inheritance

- **Dosya:** `src/Invekto.Shared/DTOs/ChatAnalysis/AnalysisCriterion.cs` (9-65)
- **Boyut:** duplication · **Fix-risk:** low · behavior-preserving
- **Sorun:** PurchaseProbabilityCriterion (lines 32-65) is a strict superset of AnalysisCriterion (lines 9-27): it adds Percentage and Color but copies the same Summary, Details, and Empty factory pattern verbatim. If the base fields or the Empty sentinel ever change (e.g. adding a new localization key to the fallback Details string), both classes must be updated in sync. The current duplication is 10 lines of identical field+factory code.
- **Kanıt:** `AnalysisCriterion (lines 9-27) and PurchaseProbabilityCriterion (lines 32-65) in AnalysisCriterion.cs — both declare 'public required string Summary { get; init; }' and 'public required string Details { get; init; }' and a matching 'static ... Empty =>' factory.`
- **Önerilen fix:** Make PurchaseProbabilityCriterion extend AnalysisCriterion by adding only the Percentage and Color properties. Override Empty in PurchaseProbabilityCriterion with the percentage-specific sentinel (0 / 'red'). Alternatively, keep the flat sealed classes but add an XML-doc cross-reference noting the relationship so future edits remember to update both.

#### Shared-22 · [LOW] QnbVPosService placed in Invekto.Shared but is consumed exclusively by Invekto.Backend — service-specific leakage into Shared

- **Dosya:** `src/Invekto.Shared/Services/QnbVPosService.cs` (1-179)
- **Boyut:** isolation · **Fix-risk:** low · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** QnbVPosService and QnbVPosSettings exist in Invekto.Shared.Services but are registered and used only in Invekto.Backend (Program.cs:566, :9467, :9525). No other service project references or injects QnbVPosService. The Shared library is a cross-service contract/utility layer; a payment gateway integration that is Backend-specific violates the isolation principle — it forces all other services to compile and carry QNB payment SDK dependencies (System.Web.HttpUtility, SHA1, HTML form builder) even though they never use them.
- **Kanıt:** `QnbVPosService.cs registration search: 'src/Invekto.Backend/Program.cs:566 builder.Services.AddSingleton<QnbVPosService>();' — only Backend. Grep for QnbVPosService across all service projects returns only Backend and Shared hits.`
- **Önerilen fix:** Move QnbVPosService and QnbVPosSettings to Invekto.Backend (e.g., src/Invekto.Backend/Payment/QnbVPosService.cs). Move PaymentDtos (src/Invekto.Shared/DTOs/Payment/PaymentDtos.cs) along with it, since PaymentInitRequest/PaymentInitResult/PaymentCallbackData/PaymentResultResponse are also only consumed by Backend endpoints. This is a pure file relocation — no behavioral change.

## 8. Ek: Refute Edilen Bulgular (40) — şeffaflık

Bunlar review'da ileri sürülüp adversarial doğrulamada elendi (false-positive / sanctioned exception / yanlış severity).

- `src/Invekto.Backend/Program.cs` — Broad catch(Exception) in ResolveTranslateTenantAsync — NpgsqlException and auto-provision DB errors conflated  
  _neden elendi:_ The specific correctness hazard claimed — "a cancelled request at line 9675 would silently fall through to auto-provision and create a new tenant row" — does not materialize. The cancellation token `ct` is passed to both `pgFactory.OpenConn
- `src/Invekto.Backend/Program.cs` — DefaultRequestHeaders used for per-request secret in FetchWapCrmInstances — cross-tenant secret leak risk on connection reuse  
  _neden elendi:_ The actual code at lines 3941-3944 creates a `new HttpClient` with `using var` — locally scoped, disposed at end of the function. This instance is never shared, never stored in a DI-registered field, never managed by IHttpClientFactory. The
- `src/Invekto.Backend/Program.cs` — Broad catch(Exception) in /ops/hangfire-login JWT validation swallows SecurityTokenException and others  
  _neden elendi:_ The claim is a false positive. `JwtValidator.ValidateToken` (c:\CRMs\InvektoServices\src\Invekto.Shared\Auth\JwtValidator.cs lines 75-95) already catches and types all JWT-related exceptions internally: `SecurityTokenExpiredException` (line
- `src/Invekto.Backend/Program.cs` — COUNT(*) result read with GetInt64 is correct but NpgsqlException not caught in Hangfire orphan-queue guard  
  _neden elendi:_ Code at the cited location is correct on both counts. Line 629 uses `Convert.ToInt64(orphanCmd.ExecuteScalar() ?? 0L)` which is the right way to read a COUNT(*) bigint result. Line 639 catches `Npgsql.NpgsqlException` which is the correct b
- `src/Invekto.Backend/Program.cs` — FetchWapCrmInstances puts secret in DefaultRequestHeaders — shared HttpClient would cause cross-tenant secret leak  
  _neden elendi:_ The code at lines 3941-3969 shows `FetchWapCrmInstances` creates a brand-new `HttpClient` with `using var httpClient = new HttpClient { ... }` on each invocation. The client is disposed at the end of the method. There is no shared/pooled cl
- `src/Invekto.Backend/Program.cs` — Flow-builder CRUD proxy endpoints do not verify route tenantId matches the JWT-bound TenantContext  
  _neden elendi:_ The claimed IDOR is not real. The Backend flow-builder CRUD proxy handlers (Program.cs lines 4289-4380) do pass the route-supplied tenantId through to the Automation service without a local JWT check, but this is a thin pass-through proxy, 
- `src/Invekto.Backend/Program.cs` — Auto-provision ON CONFLICT overwrites existing tenant's inma_code on random ID collision  
  _neden elendi:_ The core code observation is accurate — lines 9681 and 9686-9690 do generate a random tenant_id with `new Random().Next(10000000, 99999999)` and use `ON CONFLICT (tenant_id) DO UPDATE SET inma_code = @code`, which would silently overwrite a
- `src/Invekto.Backend/Program.cs` — Broad catch(Exception) in payment initiate endpoint covers NpgsqlException + application exceptions without typed mapping  
  _neden elendi:_ The claim contains a factual error that undermines its core premise. At line 9519, the log statement is: `jsonLog.SystemWarn($"Payment initiate failed ({ErrorCodes.BackendPaymentInitFailed}): ...")` — and `ErrorCodes.BackendPaymentInitFaile
- `src/Invekto.Backend/Program.cs` — Broad catch(Exception) in payment callback endpoint — form parse and ParseCallback both swallowed  
  _neden elendi:_ The finding cites real lines but contains three material inaccuracies that undermine the high-severity verdict.

(1) INV-XXX codes ARE present. Both catches log `ErrorCodes.BackendPaymentCallbackInvalid` inline. The claim "neither catch map
- `src/Invekto.Backend/Program.cs` — Broad catch(Exception) in payment callback DB update and payment history — swallows NpgsqlException without typed mapping  
  _neden elendi:_ The broad `catch (Exception ex)` pattern violation is real at both lines 9573 and 9637, but the claim's key supporting assertions are false, and the proposed fix is not behavior-preserving.

FALSE CLAIM 1 — "the DB error code is never logge
- `src/Invekto.Backend/Services/MetaLeadgen/MetaLeadgenWebhookService.cs` — AuditInsertAsync silently swallows DB failure on rejected-attempt paths — violated audit-table contract  
  _neden elendi:_ The claim misreads the contract. The class-level doc (lines 21-24) says these paths "write an audit row" — it does not promise to abort the response if that write fails. The AuditInsertAsync method (lines 304-308) has its own explicit doc t
- `src/Invekto.Backend/Services/MetaLeadgen/MetaLeadgenEndpoints.cs` — Orphaned XML summary block misattributed to wrong method — CanonicalMap has no doc comment  
  _neden elendi:_ The structural problem is real: lines 610-616 contain a summary block describing CanonicalMap, followed immediately by a second summary block (617-624) for ParseConsentBool, and both precede ParseConsentBool at line 625 while CanonicalMap a
- `src/Invekto.Backend/Services/ClaudeWizardService.cs` — Broad catch(Exception) around Task.WhenAll silently discards URL validation exceptions at Debug level  
  _neden elendi:_ The inner bare `catch` block at lines 551-554 catches every exception from `_httpClient.GetAsync` except the app-shutdown `OperationCanceledException` (which is re-thrown). This means the async lambdas passed to `Task.WhenAll` never fault u
- `src/Invekto.Outbound/Program.cs` — Auth check placed after input validation in POST /api/v1/templates — unauthenticated bad requests get 400 instead of 401  
  _neden elendi:_ The claim is factually correct about code ordering but wrong about its security impact. JwtAuthMiddleware (c:\CRMs\InvektoServices\src\Invekto.Shared\Middleware\JwtAuthMiddleware.cs lines 69-98) is registered for all /api/v1/ paths and hard
- `src/Invekto.Outbound/Data/ProjectsRepository.cs` — PostgresException-only catch in ProjectsRepository.CreateAsync/UpdateAsync leaves bulk_send_jobs.broadcast_ids array-push errors unmapped  
  _neden elendi:_ The claim is not a real bug. The repo's inner `catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)` correctly catches only UniqueViolation and re-maps it to `NameConflict`. Every other `PostgresException` (
- `src/Invekto.Outbound/Data/ProjectsRepository.cs` — RequeueForResendAsync and RequeueAllForResendAsync do not reset sent_at, creating stale send-timestamp on re-queued messages  
  _neden elendi:_ The finding's key conditional — "if the worker does NOT write sent_at on re-send" — is false. The worker calls UpdateMessageStatusAsync(msg.Id, "sent", ...) at OutboundRepository.cs:283 (MessageSenderService.cs:283). That method builds the 
- `src/Invekto.Outbound/Services/ConsentManager.cs` — ConsentManager.UpsertConsentAsync throws ArgumentException for invalid consent_type — not a typed error code  
  _neden elendi:_ The endpoint layer (Program.cs lines 1848-1855) validates consent_type and returns an INV-coded 400 (OutboundInvalidConsentPayload) BEFORE calling UpsertConsentAsync. The ArgumentException at ConsentManager.cs:121 is dead code — it cannot b
- `src/Invekto.Outbound/Services/CsvRecipientParser.cs` — CsvRecipientParser.ParseCsv LimitExceeded returns wrong TotalInput count  
  _neden elendi:_ The TotalInput field is indeed set to rows.Count (== maxRows) on the LimitExceeded early-exit path (CsvRecipientParser.cs line 48), not the real line count. However, the claimed user-visible impact does not occur. BulkSendOrchestrator.Previ
- `src/Invekto.Outbound/Services/CxapiWebhookReconcileJob.cs` — OwnedHosts() in CxapiWebhookReconcileJob allocates a new HashSet on every reconciliation tick per tenant  
  _neden elendi:_ The allocation is real but deliberately not cached: the class-level doc comment (lines 14-17) explicitly states "NO confirmed-cache — that would mask drift if the webhook is changed externally; with a tiny tenant set the read each tick is n
- `src/Invekto.Outbound/Services/ProjectsService.cs` — ProjectsService.BuildSkippedParamsInfo calls GetInt32 on a jsonb integer value without cast — potential InvalidCastException  
  _neden elendi:_ The claim is a false positive. The `by_param` counts in the JSON are individual parameter-miss counts, each bounded by `SkippedMissingParams` (the total skipped count). The SQL at BulkSendRepository.cs:583 explicitly casts the total to `::i
- `src/Invekto.Automation/Program.cs` — Broad catch(Exception) in SendErrorCallbackAsync helper — hard-fail pattern  
  _neden elendi:_ The broad catch(Exception) at line 612-615 is inside a best-effort fire-and-forget error-notification helper (`SendErrorCallbackAsync`) that is itself called from an unobserved `_ = Task.Run(...)` background task (line 561). In this structu
- `src/Invekto.Automation/Program.cs` — Null-forgiving operator (!) on result.ErrorCode/ErrorMessage — hard-fail pattern  
  _neden elendi:_ The `!` operators at lines 1425 and 1488 are used on `result.ErrorCode` and `result.ErrorMessage`, which are typed `string?` on `SimulationStartResult` / `SimulationStepResult` (SimulationEngine.cs lines 380-381 and 401-402). However, every
- `src/Invekto.Automation/Data/AutomationRepository.cs` — FOR UPDATE SKIP LOCKED used outside a transaction — lock is released immediately  
  _neden elendi:_ Both `GetPendingFollowUpsAsync` (line 1332) and `MarkFollowUpSentAsync` (line 1359) are dead code. A full-codebase grep confirms they appear only in `AutomationRepository.cs` as declarations — they have zero callers anywhere in the `src/` t
- `src/Invekto.Automation/Data/AutomationRepository.cs` — CreateFlowVersionAsync: SELECT MAX + INSERT is subject to concurrent version-number collision  
  _neden elendi:_ The claim rests on two sub-claims, both of which are refuted by the actual code and schema.

Sub-claim 1 — "23505 is unhandled and propagates as an untyped exception": FALSE. The call site at Program.cs:848-856 wraps CreateFlowVersionAsync 
- `src/Invekto.Automation/Data/AutomationRepository.cs` — ListMonitorExecutionsAsync: phone LIKE filter is a partial SQL injection vector via string interpolation  
  _neden elendi:_ The code is fully parameterized and contains no SQL injection vector. Every predicate appended to the `where` StringBuilder is a static string literal containing only named parameter placeholders (e.g., `" AND e.status = @status"`, `" AND e
- `src/Invekto.Automation/Services/AutomationOrchestrator.cs` — Broad catch(Exception) swallows OperationCanceledException in ProcessMessageAsync outer handler  
  _neden elendi:_ The claim's core behavioral assertion — that OCE is swallowed during graceful shutdown causing "delay service shutdown and cause job-queue re-enqueue loops" — is refuted by the only call site (Program.cs line 565), which explicitly passes C
- `src/Invekto.Automation/Services/AutomationOrchestrator.cs` — Broad catch(Exception) in fire-and-forget SendAutoTagCallbackAsync  
  _neden elendi:_ The broad `catch (Exception)` at line 1641 is real, but the claim is rejected for two reasons that undermine its validity.

First, the proposed fix's core rationale is wrong: the method is called as `_ = SendAutoTagCallbackAsync(...)` (line
- `src/Invekto.Automation/Services/AutomationOrchestrator.cs` — Null-forgiving operator on action.ReplyText hides real NullReferenceException risk  
  _neden elendi:_ The claim is a false positive. FlowConfig.UnknownInputMessage is declared as `required string` (non-nullable, line 211) and ParseFlowConfig at line 195 always assigns it with a `?? DefaultUnknownInput` double-guard — both when the JSON key 
- `src/Invekto.Automation/Services/MarketingRescueClient.cs` — GetPriorRiskCountAsync fetches all tenant risks and filters phone+cutoff client-side  
  _neden elendi:_ The client-side filtering at lines 91-93 is real and confirmed. However, the correctness framing — specifically the claim that pagination will silently underreport prior risk history — is speculative. There is no evidence in the codebase th
- `src/Invekto.Shared/Contracts/Inma/HttpInmaContactOptOutClient.cs` — HttpInmaContactOptOutClient sets DefaultRequestHeaders instead of per-request header — violates cross-tenant isolation contract  
  _neden elendi:_ The DefaultRequestHeaders usage is real (lines 35-38 of HttpInmaContactOptOutClient.cs), and the dead `_secretKey` field observation is accurate. However the security framing does not hold. This client is wired as a background sweep job (In
- `src/Invekto.Shared/Services/TenantCampaignConfigValidator.cs` — Slug regex comment/error message says '2-64 chars' but regex allows exactly 1-char slug (minimum-length inconsistency)  
  _neden elendi:_ After reading the actual file, the claim is self-refuting. Line 14 of the doc comment reads "Slug regex: lowercase start, [a-z0-9_-]{1,63}" — it explicitly separates the leading-char requirement ("lowercase start") from the suffix quantifie
- `src/Invekto.Shared/Services/TenantCampaignConfigValidator.cs` — ValidateDates builds citySlugLookup including empty-string fallback for null city slugs, making an empty-city date entry incorrectly pass the cross-reference check  
  _neden elendi:_ The code at lines 231-253 does contain the `?? string.Empty` defensive fallback in both the lookup construction and the per-entry check, exactly as described. However, the bug cannot occur in any real execution path. `ValidateDates` is `pri
- `src/Invekto.Automation/Services/CampaignTemplateApplier.cs` — CampaignTemplateApplier passes campaignSlug: null to both IsWithinWindowAsync and RenderPlaceholderAsync, but the window-guard and substitution use different active-campaign resolution paths that can diverge  
  _neden elendi:_ The claimed divergence does not hold given the actual code structure.

1. Both IsWithinWindowAsync and RenderPlaceholderAsync hit the same GetOrFetchAsync call with the same tenantId and the same nowUtc, so they operate on the identical cac
- `arch/errors.md` — INV-AUTH-004: documented in errors.md but no constant defined in ErrorCodes.cs  
  _neden elendi:_ The gap is real — ErrorCodes.cs (lines 149-154) jumps from INV-AUTH-003 directly to INV-AUTH-005, skipping INV-AUTH-004, which is documented in errors.md (lines 231-233). However, a grep of the entire src/ tree finds zero occurrences of the
- `arch/errors.md` — INV-WC-013..025 and INV-INT-100..147: documented in errors.md but no constants in ErrorCodes.cs  
  _neden elendi:_ The claim has three distinct parts, all of which are false positives or based on incomplete investigation.

1. INV-INT-140..147 (VCP codes): These ARE defined as C# constants — not in ErrorCodes.cs, but in two intentional service-local file
- `arch/errors.md` — INV-VAL-001..003 and INV-EXT-001..002: documented in errors.md but no constants in ErrorCodes.cs  
  _neden elendi:_ The claim is technically accurate in that INV-VAL-001..003 and INV-EXT-001..002 appear in arch/errors.md (confirmed at lines 768-778 and 1591-1597) but have no constants in ErrorCodes.cs (which runs to 771 lines with no VAL or EXT namespace
- `src/Invekto.Shared/DTOs/Outbound/TemplateDtos.cs` — Duplicate class names TemplateCreateRequest and TemplateUpdateRequest across two namespaces  
  _neden elendi:_ The duplicate class names are real — `TemplateCreateRequest` and `TemplateUpdateRequest` exist in both `Invekto.Shared.DTOs.Outbound` (TemplateDtos.cs:8,32) and `Invekto.Shared.DTOs.Templates` (TemplateCatalogDtos.cs:82,127) with structural
- `src/Invekto.Shared/Services/KvkkHelper.cs` — KvkkHelper.IsHealthTenant calls TryGetProperty twice on the same element — redundant parse  
  _neden elendi:_ The double TryGetProperty call at lines 65 and 72 is real — the first call succeeds (returns true, property found) but the ValueKind check fails, so `prop` is discarded and TryGetProperty is called again at line 72. The proposed single-read
- `src/Invekto.Shared/Services/TenantUsageService.cs` — TenantUsageService.SeedFromDbAsync: messages_sent read via ExecuteScalarAsync cast 'result is int' — silently returns 0 if DB returns long/bigint at runtime  
  _neden elendi:_ The claim is confirmed safe in the actual codebase. The tenant_usage.messages_sent column is defined as INTEGER (not BIGINT) in arch/db/tenant-usage.sql line 13, and the migration DDL confirms this has not changed. Npgsql maps PostgreSQL IN
- `src/Invekto.Shared/Contracts/Inma/HttpInmaContactOptOutClient.cs` — HttpInmaContactOptOutClient sets X-CIB-SecretKey on DefaultRequestHeaders — correct only because it is a typed singleton client, but violates the per-request secret pattern mandated for multi-tenant clients  
  _neden elendi:_ The DefaultRequestHeaders usage is intentional and architecturally correct for this client. The DI registration (Invekto.Outbound/Program.cs lines 182-190) uses AddHttpClient typed-client pattern with a single global secret from appsettings

---
_Üretim: read-only multi-agent audit (core-services-audit), 2026-06-14. Kod değişikliği yapılmadı._
