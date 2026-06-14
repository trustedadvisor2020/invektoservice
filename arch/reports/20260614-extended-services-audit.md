# Genişletilmiş Servis Refactor Audit — 2026-06-14 (kalan 10 servis)

> **Kapsam:** Integrations · Marketing · Knowledge · ChatAnalysis · WebChat · Appointments · AgentAI · WhatsAppAnalytics · VoiceRuntime · VoiceAI — ~44k satır / 162 dosya (10 servis).
> **Yöntem:** READ-ONLY. Sıfır dosya yazımı, sıfır commit, sıfır deploy. Work unit başına review (sonnet) → her bulgu adversarial refute (sonnet) → sentez (opus).
> **İstatistik:** 106 ham bulgu → **78 doğrulanmış** (28 refute/elendi).
> **Kullanım:** Triage backlog'u. Her madde behavior-preserving fix önerisi + fix-risk taşır. Bunlar LLM-bulgusu + LLM-doğrulaması; build/Codex ile DOĞRULANMADI. Onayladığın maddeler `auto`+Codex döngüsünden geçer (gerçek kapı orası).

## 1. Yönetici Özeti

Overall health: structurally sound but with a consistent, mechanically-fixable defect layer. Across 10 services there are NO architectural rot signals (no microservice-isolation violations, no duplicated DTOs across services, no cross-service project references). The platform's design holds. The rot is concentrated in two places and is repetitive rather than deep: (1) data-access type handling — COUNT(*) bigint read via GetInt32 and null-forgiving operators on ExecuteScalar results, which are latent runtime crashes (InvalidCastException / NullReferenceException) sitting on live request paths; and (2) error-handling discipline — broad catch(Exception) blanketing DB calls so NpgsqlException is mis-mapped to domain error codes (OpenAI/Storage/Upload errors), which both violates the repo hard-fail rule and corrupts incident diagnosis. The single worst concentration is WhatsAppAnalytics/Program.cs (30+ broad catches on DB endpoints) and Knowledge/Program.cs (DB endpoints split between broad-catch and zero-catch). The highest-severity individual bugs are crash-on-every-call defects that look like they have not yet been exercised in production: VoiceAI raw TraceIdentifier+filename in a Windows path (colon crashes every transcription AND allows path traversal), Appointments and Knowledge COUNT(*) GetInt32 crashes, and the Knowledge GetPublishedForComparisonAsync column-index mismatch (IndexOutOfRangeException). Security posture has two genuine fail-open defaults worth prioritizing (ChatAnalysis auth disabled when key empty; WhatsAppAnalytics OpsKey open by default). Net: this is a healthy codebase with a thin but widespread crust of mechanical defects — high-ROI to clean because most fixes are trivial and behavior-preserving, and the structural foundation does not need touching.

## 2. Severity Dağılımı

| Servis | Critical | High | Medium | Low | Toplam |
|---|---|---|---|---|---|
| WhatsAppAnalytics | 0 | 5 | 9 | 3 | 17 |
| Knowledge | 1 | 3 | 5 | 7 | 16 |
| AgentAI | 0 | 0 | 6 | 3 | 9 |
| Appointments | 1 | 3 | 1 | 3 | 8 |
| Integrations | 1 | 3 | 1 | 1 | 6 |
| ChatAnalysis | 0 | 2 | 1 | 3 | 6 |
| WebChat | 0 | 2 | 4 | 0 | 6 |
| VoiceRuntime | 0 | 0 | 0 | 4 | 4 |
| VoiceAI | 1 | 1 | 0 | 2 | 4 |
| Marketing | 0 | 1 | 0 | 1 | 2 |
| **TOPLAM** | **4** | **20** | **27** | **27** | **78** |

**Boyut dağılımı:** error-handling (34) · correctness (26) · security (9) · duplication (9)

## 3. Cross-Cutting Pattern'ler (en yüksek kaldıraç)

### 3.1 COUNT(*) bigint read via GetInt32 — latent InvalidCastException on live request paths _[critical]_

- **Servisler:** Integrations, Knowledge, Appointments
- **Pattern:** COUNT(*) bigint read via GetInt32 — latent InvalidCastException on live request paths. PostgreSQL COUNT(*) (and COALESCE(COUNT(*),0)) returns int8/bigint; GetInt32 throws at runtime. These are crash-on-call defects, not style nits.
- **Öneri:** Two equivalent fixes, both behavior-preserving: cast in SQL (COUNT(*)::int / COALESCE(...,0)::int) OR read with GetInt64 and narrow in C#. Prefer ::int cast at the query when the count cannot exceed int range (true here — per-tenant slot/template counts). Grep every '::int' absence around COUNT( and every GetInt32 reading a count column across all 16 services, not just the 10 audited — this pattern almost certainly exists in unaudited services too.

### 3.2 Broad catch(Exception) wrapping DB calls — repo hard-fail rule violated AND NpgsqlException mis-mapped to wron _[high]_

- **Servisler:** Knowledge, ChatAnalysis, AgentAI, WebChat, WhatsAppAnalytics, Integrations
- **Pattern:** Broad catch(Exception) wrapping DB calls — repo hard-fail rule violated AND NpgsqlException mis-mapped to wrong INV-XXX domain codes. The damage is twofold: it breaks the team's own review gate, and it corrupts diagnosis (a DB outage surfaces as KnowledgeOpenAIError / WAStorageError / KnowledgeUploadFailed, sending the on-call down the wrong path).
- **Öneri:** Standardize on a typed ladder: catch NpgsqlException (base, NOT just PostgresException) -> map to a DB INV code; catch the domain-specific exception (HttpRequestException for upstream, JsonException for parse) -> its own code; let OperationCanceledException propagate (several broad catches currently swallow cancellation, which is a separate latent bug in worker loops and health checks). Do this as a guided sweep, file-by-file, NOT a bulk regex rewrite — each catch needs the correct domain code chosen by hand. Highest density first: WhatsAppAnalytics/Program.cs and Knowledge/Program.cs.

### 3.3 Catch-only-PostgresException leaves the rest of NpgsqlException unhandled _[medium]_

- **Servisler:** Knowledge, Appointments
- **Pattern:** Catch-only-PostgresException leaves the rest of NpgsqlException unhandled. Several endpoints catch ONLY PostgresException with SqlState 23505 (unique violation) and let connection/timeout/transient NpgsqlException escape unmapped — the inverse failure mode of the broad-catch pattern, same root cause (NpgsqlException base not used).
- **Öneri:** Keep the 23505 special-case for the friendly 'duplicate' message, but add an outer catch (NpgsqlException) -> generic DB INV code so transient errors are mapped, not 500'd raw. Trivial and behavior-preserving for the success path.

### 3.4 Null-forgiving operator (!) on ExecuteScalarAsync results — repo hard-fail; NRE if the scalar is ever NULL/abs _[high]_

- **Servisler:** WhatsAppAnalytics, Appointments
- **Pattern:** Null-forgiving operator (!) on ExecuteScalarAsync results — repo hard-fail; NRE if the scalar is ever NULL/absent. Concentrated in WhatsAppAnalytics AnalyticsRepository and Appointments BookAppointment.
- **Öneri:** Replace `result!` with an explicit guard: `result is null ? throw new InvektoException(INV-...) : Convert.ToX(result)`, or pattern-bind `if (result is long n)`. Behavior-preserving in the happy path; converts a silent NRE into a coded error on the edge case.

### 3.5 Per-request secret set via DefaultRequestHeaders / Headers _[medium]_

- **Servisler:** ChatAnalysis, Knowledge, AgentAI, VoiceAI
- **Pattern:** Per-request secret set via DefaultRequestHeaders / Headers.Add instead of HttpRequestMessage + TryAddWithoutValidation. Cross-tenant secret-leak risk (shared HttpClient) and FormatException risk on malformed keys. Spread thin across services.
- **Öneri:** Move secret/auth headers onto a per-call HttpRequestMessage with TryAddWithoutValidation. ChatAnalysis WapCrmClient (DefaultRequestHeaders) is the genuine cross-tenant-leak shape and is highest priority — but note that client is also flagged as dead code (never registered), so deleting it may be the real fix. The Headers.Add cases (Knowledge EmbeddingService, AgentAI ReplyGenerator, VoiceAI) are FormatException-hardening only, lower urgency.

### 3.6 Fail-open security defaults — auth silently disabled when its config key is empty/unset _[high]_

- **Servisler:** ChatAnalysis, WhatsAppAnalytics
- **Pattern:** Fail-open security defaults — auth silently disabled when its config key is empty/unset. A misconfigured production instance exposes protected surface with no error.
- **Öneri:** Invert the default to fail-closed: if the key is empty in a Production environment, refuse to start (or reject all protected requests) rather than waving traffic through. ChatAnalysis /api/v1/analyze (internal API key) and WhatsAppAnalytics OpsKey (/api/ops/*) are the two. Low fix-risk but NOT behavior-preserving by design — that is the point; coordinate with deploy config so prod actually has the keys set before flipping.

## 4. Quick Wins (önce bunlar — düşük risk, behavior-preserving)

- **Fix VoiceAI temp-path crash + path traversal (single line, two bugs)** — `src/Invekto.VoiceAI/Services/VoiceTranscriptionService.cs:42`  
  Verified: Path.Combine(Path.GetTempPath(), $"voiceai_{requestId}_{fileName}") puts raw TraceIdentifier (contains ':' on Windows -> invalid path, crashes EVERY transcription) and user-controlled fileName (path traversal -> arbitrary file write) into the path. Sanitize both: strip non-[A-Za-z0-9_-] from requestId, and use Path.GetFileName(fileName) + a guid extension. Trivial, behavior-preserving for legitimate input, closes a critical crash and a high security hole at once.
- **Cast COUNT(*) to ::int (or read GetInt64) in the three confirmed crash sites** — `src/Invekto.Appointments/Data/AppointmentsRepository.cs:700; src/Invekto.Knowledge/Services/TemplateExtractorService.cs:318-334; src/Invekto.Integrations/Data/IntegrationsRepository.cs:562-580`  
  Each reads a bigint count via GetInt32 -> guaranteed InvalidCastException when the path runs. Appointments one is on GetAvailableSlots (booking flow). One-line SQL ::int cast per site, behavior-preserving.
- **Fix Knowledge GetPublishedForComparisonAsync column-index mismatch** — `src/Invekto.Knowledge/Data/TemplateRepository.cs:787-801`  
  Verified: the SELECT lists 21 columns (0-20) and omits group_tag, but ReadCatalogDto reads group_tag at index 21 (per the explicit FEAT-WTP comment at line 24) -> IndexOutOfRangeException whenever this comparison path runs. Add `, group_tag` to the SELECT to match the reader contract. Trivial.
- **WAA AvgOverallScore returns SUM not AVG below 100 rows** — `src/Invekto.WhatsAppAnalytics/Data/InsightRepository.cs:1353-1399`  
  Verified: totalOverall is a running SUM; the true DB AVG only replaces it when totalCount==100. For <100 scored conversations the reported AvgOverallScore is the sum -> a customer-facing quality metric is wrong for every small tenant. Divide totalOverall by scores.Count in the <100 branch. Trivial, fixes a real reporting bug.
- **SlowEquals timing leak + length early-exit** — `src/Invekto.Integrations/Endpoints/VideoMeetingEndpoints.cs:143-150`  
  Constant-time compare that early-returns on length mismatch leaks secret length via timing. Use CryptographicOperations.FixedTimeEquals (or compare into a fixed-size buffer). Trivial, behavior-preserving.
- **Delete or wire up dead WapCrmClient** — `src/Invekto.ChatAnalysis/Services/WapCrmClient.cs:1-145`  
  Defined but never registered/used (medium finding). It is ALSO the worst per-request-secret offender (DefaultRequestHeaders for X-CIB-SecretKey). Deleting it removes ~145 lines of dead code and the secret-leak shape in one move — confirm zero references first, then remove.
- **Add `::int` / NpgsqlException base to the catch-23505-only endpoints** — `src/Invekto.Appointments/Program.cs:765-776; src/Invekto.Knowledge/Program.cs:1139-1150`  
  Both catch ONLY PostgresException 23505 and let other DB errors escape unmapped. Add an outer catch(NpgsqlException) -> generic DB code. Trivial, success path unchanged.

## 5. Yüksek-Değerli Refactor'lar

- **Service-wide typed-catch sweep replacing broad catch(Exception) on DB paths**
  - Kapsam: WhatsAppAnalytics/Program.cs (30+ sites), Knowledge/Program.cs (~17 sites split broad-catch / zero-catch), plus AgentAI, WebChat, ChatAnalysis, Integrations background handlers. Introduce one shared helper/extension that maps NpgsqlException->DB INV code, rethrows domain exceptions, and lets OperationCanceledException propagate; then convert call sites file-by-file.
  - Kazanım: Closes the single largest repeating defect class, restores the team's own review-gate invariant, and stops DB outages from masquerading as OpenAI/Storage/Upload errors (faster, correct incident diagnosis). Also fixes the swallowed-OperationCanceledException worker-loop/health-check bugs that ride along with this pattern.
  - Risk: Medium. NOT a mechanical regex job — each catch needs the correct domain INV code chosen by hand, and a few currently-swallowed errors will start surfacing as coded failures (intended, but changes observable behavior on error paths). Do it per-service with /rev gating, not in one bulk commit. Start with WhatsAppAnalytics (highest density, lowest coupling — analytics read paths).
- **Centralize DB scalar/count reading into a typed helper**
  - Kapsam: A small Invekto.Shared helper (e.g. ReadCount -> long, RequireScalar<T> with explicit null guard) adopted across all repositories. Replaces ad-hoc GetInt32-on-COUNT and result!-on-ExecuteScalar across Integrations, Knowledge, Appointments, WhatsAppAnalytics.
  - Kazanım: Eliminates the COUNT-GetInt32 crash class and the null-forgiving-scalar NRE class at the source, and prevents reintroduction. Turns a recurring review-failure pattern into a one-call convention.
  - Risk: Low-medium. The helper itself is trivial and behavior-preserving; risk is purely in breadth of touch (many files) and that it lives in Shared (so a build touches every service). Roll out incrementally — helper first, then migrate the confirmed crash sites, then opportunistically the rest. Service-isolation is preserved (Shared is the sanctioned channel).
- **Fix-the-default on the two fail-open auth gates**
  - Kapsam: ChatAnalysis internal-API-key gate (Program.cs:92-101) and WhatsAppAnalytics OpsKey (Program.cs:167,771-775): refuse to serve protected routes when the key is unset in Production.
  - Kazanım: Removes two genuine 'one missing env var = wide-open endpoint' exposures. High security payoff for small code.
  - Risk: Low code risk but deliberately behavior-CHANGING, and operationally sharp: if any current prod instance is silently relying on the empty-key bypass, flipping to fail-closed will break it. Verify via deploy config that all 11 tenants/instances actually have the keys set BEFORE shipping; gate behind ASPNETCORE_ENVIRONMENT==Production so dev stays frictionless.
- **Appointments slot-capacity TOCTOU hardening**
  - Kapsam: src/Invekto.Appointments/Program.cs:439-456 — capacity check and INSERT run on separate connections with no serialization, so concurrent bookings can oversell a slot.
  - Kazanım: Closes a real double-booking race in the core booking flow (healthcare clinics — the dominant segment). Correctness payoff is high for the business.
  - Risk: Medium and genuinely not behavior-preserving under contention — needs a single-connection transaction with either SELECT ... FOR UPDATE on the slot row or a DB unique/exclusion constraint + handle-the-conflict. Must be load/concurrency tested, not just compiled. Treat as its own scoped task with Q sign-off, not part of a sweep.

## 6. DOKUNMA (stabil — refactor risk-karşılığı-getirisiz)

- Microservice boundaries / project structure — the audit found ZERO isolation violations, ZERO cross-service project references, ZERO duplicated-DTO-across-services. The architecture is sound; do not 'tidy' it.
- Backend.csproj ProjectReference with PrivateAssets=all (sanctioned scheduler-host reflection pattern) — explicitly not a violation; leave it.
- Background/hosted SWEEP jobs that query across all tenants without a tenant_id filter — sanctioned by design (background != request path). Do not add tenant scoping to sweeps.
- Documented unauthenticated INMA-callback contracts (e.g. /api/v1/callback/wapcrm) — auth absence is contractual; do not 'fix' it by adding auth.
- VoiceRuntime LatencyTracker window-trim-outside-lock (Metrics/LatencyTracker.cs:56-63) and the /ready 200-on-degraded nit — purely cosmetic / micro-races with no real-world impact; refactoring risks the hot voice path for no payoff.
- WhatsAppAnalytics TemplateRepository NpgsqlCommand not-disposed-on-error-path — connections are pooled and the leak is per-error-only; low real-world impact. Fold into the typed-catch sweep IF the file is already open, but do not open it just for this.
- Low-value duplication nits with no divergence risk yet (InferCategory copy, SafeGet helper copy, duplicate compiled Regex, double JSON enumeration) — real but trivial; only collapse them if you are already editing the file for a higher-value reason. Not worth a standalone change or the /rev cost.

## 7. Doğrulanmış Bulgular (servis → severity sırası)

### WhatsAppAnalytics (17)

#### WhatsAppAnalytics-1 · [HIGH] Broad catch(Exception) on all DB-path endpoint handlers — hard-fail pattern

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Program.cs` (535,560,587,617)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** The list-analyses, get-analysis, delete-analysis, and get-metadata endpoints all catch bare `Exception` instead of typed exceptions. Per repo hard-fail rules, DB-path catches must use `NpgsqlException` (base) so that Npgsql-level errors (e.g. connection reset, SSL renegotiation) are explicitly mapped to INV-error codes rather than silently absorbed. A non-DB exception (e.g. OperationCanceledException from a cancelled request) will be returned as a 500 WADatabaseError, hiding the real cause.
- **Kanıt:** `catch (Exception ex) { return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, $"List failed: {ex.Message}" ...) (line 535); same pattern at 560, 587, 617`
- **Önerilen fix:** Replace each `catch (Exception ex)` in these four handlers with `catch (NpgsqlException ex)` mapping to WADatabaseError + a separate `catch (OperationCanceledException) { return Results.StatusCode(499); }` so request-cancelled paths are distinguished from DB errors. For truly unexpected exceptions let them propagate to the ASP.NET unhandled-exception middleware.

#### WhatsAppAnalytics-2 · [HIGH] Broad catch(Exception) on all insight compute and read endpoints — hard-fail pattern

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Program.cs` (1078,1108,1143,1173,1208,1238,1277,1307,1346,1380,1415,1445,1484,1516,1559,1591,1628,1651,1687,1718,1945,2033,2067,2116,2158,2210)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** Every insight compute (response-time, agent-leaderboard, rescue, demand-heatmap, revenue-attribution, objection-map, quality-score), every template mining, bulk orchestration, RI dashboard, feedback, onboarding, and template CRUD handler catches bare `Exception`. These all make PostgreSQL calls via repository services; the hard-fail rule requires a typed `NpgsqlException` catch for DB errors. As written, an `OperationCanceledException` from a client disconnect is returned as 500 WAInsightComputeFailed, and NpgsqlExceptions from unrelated errors (SSL, connection pool exhaustion) get the same treatment as a business-logic error.
- **Kanıt:** `catch (Exception ex) { jsonLogger.SystemError(...); return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, ...) at each of the listed lines`
- **Önerilen fix:** For each compute handler: add a typed `catch (NpgsqlException ex)` before the broad `catch (Exception ex)`, mapping it to WADatabaseError. Add `catch (OperationCanceledException)` returning 499. Remove the catch-all or re-throw in it after logging, so unknown exceptions propagate. For read-only GET handlers that only query PG, replacing `catch (Exception)` with `catch (NpgsqlException)` plus a re-throw is sufficient.

#### WhatsAppAnalytics-3 · [HIGH] OpsKey open by default — any unconfigured production instance exposes all /api/ops/* endpoints

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Program.cs` (167,771-775)
- **Boyut:** security · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** `benchmarkOpsKey` defaults to `""` when `Benchmark:OpsKey` is absent from config (line 167). `ValidateOpsKey` explicitly returns `true` when the key is empty (line 773, comment: 'No key = open (dev mode)'). Every `/api/ops/*` endpoint — including batch classification start (which triggers MSSQL reads), insight compute that writes PG, bulk template mining, and ground-truth label mutation — is completely unprotected if the config key is missing. A misconfigured production appsettings leaves ~25 mutation endpoints unauthenticated.
- **Kanıt:** `var benchmarkOpsKey = builder.Configuration["Benchmark:OpsKey"] ?? ""; (line 167) ... if (string.IsNullOrEmpty(benchmarkOpsKey)) return true; // No key = open (dev mode) (line 773)`
- **Önerilen fix:** Remove the 'open in dev mode' branch. Instead, throw an `InvalidOperationException` at startup (alongside the existing pgConnStr/jwtSecretKey checks) if `benchmarkOpsKey` is empty — or default to a generated random key logged at startup warning level so the dev can discover it. In production appsettings validation should catch the missing key. At minimum add a startup log warning: `logger.SystemWarn("Benchmark:OpsKey not configured — /api/ops/* endpoints are OPEN")` so it is visible in production logs.

#### WhatsAppAnalytics-4 · [HIGH] GetQualityInsightAsync returns SUM instead of AVG for AvgOverallScore when fewer than 100 rows

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Data/InsightRepository.cs` (1353-1399)
- **Boyut:** correctness · **Fix-risk:** trivial · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** In the per-conversation branch (groupByAgent=false), totalOverall accumulates the sum of individual overall_score values (line 1360: totalOverall += overall). When totalCount < 100 (the common case for most tenants), this sum is returned directly as AvgOverallScore at line 1398 without dividing by scores.Count. For a tenant with 10 conversations each scoring 75.0, AvgOverallScore is returned as 750.0 instead of 75.0. The bug is masked only when totalCount == 100, in which case the DB-computed AVG replaces totalOverall (line 1388). The <100 path is the dominant path for this production system (35 healthcare customers, analytics are per-tenant subset).
- **Kanıt:** `double totalOverall = 0; ... totalOverall += overall; // line 1360 — accumulates sum, not avg\nAvgOverallScore = scores.Count > 0 ? Math.Round(totalOverall, 1) : 0  // line 1398 — emits sum as if it were avg`
- **Önerilen fix:** Replace line 1398 with: AvgOverallScore = scores.Count > 0 ? Math.Round(totalOverall / scores.Count, 1) : 0. Keep the totalCount==100 branch unchanged — it correctly overwrites totalOverall with the DB AVG and totalCount with the real row count. Only the <100 path needs the division. Alternatively, remove the SUM accumulation entirely for the common path and always use a single SELECT AVG()::REAL, COUNT(*)::INT query before fetching the top-100 page.

#### WhatsAppAnalytics-5 · [HIGH] Null-forgiving operator (!) on COUNT(*) result in ListAnalysesAsync — hard fail

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Data/AnalyticsRepository.cs` (94)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving
- **Sorun:** COUNT(*) returns bigint; the cast chain (int)(long)result! is correct for the type, but the null-forgiving operator ! on ExecuteScalarAsync's object? return is a hard fail per repo policy. ExecuteScalarAsync returns null when the result set is empty; the ! suppresses the null-reference warning rather than guarding it.
- **Kanıt:** `var total = (int)(long)(await countCmd.ExecuteScalarAsync(ct))!;`
- **Önerilen fix:** var raw = await countCmd.ExecuteScalarAsync(ct); var total = raw is long l ? (int)l : 0;

#### WhatsAppAnalytics-6 · [MEDIUM] catch(Exception) in MSSQL import body parse (line 477) masks non-JSON exceptions

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Program.cs` (469-479)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The JSON body parsing in the `/import-mssql` endpoint uses `catch (Exception ex)` to respond with 400 WACsvParseError. If the exception is not a `JsonException` (e.g. it is a body-read `IOException` or `OperationCanceledException`), the caller receives a misleading 400 'Invalid request body' instead of a 500 or 499. This also deviates from the hard-fail rule requiring typed catches.
- **Kanıt:** `catch (Exception ex) { return Results.Json(ErrorResponse.Create(ErrorCodes.WACsvParseError, $"Invalid request body: {ex.Message}", requestId), statusCode: 400); } at line 477-479`
- **Önerilen fix:** Replace with `catch (JsonException ex)` for 400 and add a separate `catch (OperationCanceledException)` returning 499. Let `IOException` and other unexpected exceptions propagate so they surface as 500 via unhandled middleware.

#### WhatsAppAnalytics-7 · [MEDIUM] catch(Exception) swallowed in upload outer handler masks non-storage errors as WAStorageError

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Program.cs` (433-443)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** The outer `catch (Exception ex)` in the upload handler (line 433) maps any failure — including an `OperationCanceledException` from a disconnected client, or a `NpgsqlException` from `repo.CreateAnalysisAsync` — to 500 WAStorageError with the message 'File upload failed'. A DB failure during `CreateAnalysisAsync` will delete the file but report a storage error rather than a database error, obscuring the root cause in production logs.
- **Kanıt:** `catch (Exception ex) { ... jsonLogger.StepError($"[{ErrorCodes.WAStorageError}] Upload failed: {ex.Message}", requestId); return Results.Json(ErrorResponse.Create(ErrorCodes.WAStorageError, "File upload failed", requestId), statusCode: 500); } lines 433-443`
- **Önerilen fix:** Add typed inner catches: `catch (NpgsqlException ex)` mapping to WADatabaseError (file cleanup still runs), `catch (OperationCanceledException)` returning 499, and keep `catch (Exception)` only for genuinely unexpected I/O errors mapping to WAStorageError. This preserves the file-cleanup logic while returning the correct error code.

#### WhatsAppAnalytics-8 · [MEDIUM] UpsertRescueCandidatesBatchAsync ON CONFLICT omits rescue_status — silently preserves stale 'triggered' status if Delete+Upsert contract is ever violated

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Data/InsightRepository.cs` (466-475)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: low → düzeltildi)_
- **Sorun:** The ON CONFLICT UPDATE clause (lines 466-472) intentionally does not reset rescue_status to 'pending'. The surrounding code assumes callers always call DeleteRescueCandidatesAsync before UpsertRescueCandidatesAsync. If a recompute is ever triggered without a preceding delete (e.g., partial retry, future code path), a conversation whose rescue was already 'triggered' will retain that status even though it was re-evaluated as a candidate. GetRescueCandidatesAsync filters on rescue_status = 'pending' (line 488), so the recomputed record would become invisible. The risk is latent, not current.
- **Kanıt:** `ON CONFLICT (tenant_id, conversation_id) DO UPDATE SET instance_id=EXCLUDED.instance_id, outcome_label=EXCLUDED.outcome_label, ... computed_at=NOW() -- no rescue_status = 'pending' reset`
- **Önerilen fix:** Add rescue_status = 'pending' to the ON CONFLICT DO UPDATE SET clause. This is safe because the write path is a recompute; any legitimate 'triggered' status would be re-set during the recompute sweep anyway. Alternatively, document the Delete-first invariant as a code comment and add an assertion/guard in the caller.

#### WhatsAppAnalytics-9 · [MEDIUM] Broad catch(Exception) in CheckConnectionAsync — hard fail

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Data/AnalyticsRepository.cs` (41-45)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** catch (Exception ex) is unconditional and swallows any throwable including OperationCanceledException, OutOfMemoryException, and thread-abort. The repo policy explicitly forbids broad catch(Exception) even with a when() filter; every catch must be a typed exception with an INV-XXX error code mapping.
- **Kanıt:** `catch (Exception ex) { _logger.SystemWarn($"[AnalyticsRepository] Health check failed: {ex.Message}"); return false; }`
- **Önerilen fix:** Replace with: catch (NpgsqlException ex) { _logger.SystemWarn($"[AnalyticsRepository] Health check failed: {ex.Message}"); return false; } catch (OperationCanceledException) { return false; } — do NOT suppress OperationCanceledException silently if the caller passes a cancellation token and expects it to propagate.

#### WhatsAppAnalytics-10 · [MEDIUM] NpgsqlCommand not disposed on error path in TemplateRepository Upsert* methods

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Data/TemplateRepository.cs` (51-87,152-173,234-266,321-357,419-447,509-537)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving
- **Sorun:** In every Upsert* method (UpsertIntentsAsync, UpsertFaqsAsync, UpsertFlowsAsync, UpsertObjectionHandlersAsync, UpsertFollowupTemplatesAsync, UpsertOnboardingStepsAsync), the NpgsqlCommand is created with var cmd = conn.CreateCommand() (no using declaration) and disposed via explicit await cmd.DisposeAsync() at the end of the loop body. If ExecuteNonQueryAsync throws (e.g. a constraint violation, NpgsqlException, or CancellationToken), DisposeAsync is never called and the command's unmanaged resources are leaked for the lifetime of the connection.
- **Kanıt:** `var cmd = conn.CreateCommand(); ... await cmd.ExecuteNonQueryAsync(ct); await cmd.DisposeAsync(); — DisposeAsync is only reached on the happy path.`
- **Önerilen fix:** Wrap the command in an await using declaration: await using var cmd = conn.CreateCommand(); — then remove the explicit await cmd.DisposeAsync() call. The compiler guarantees disposal in both success and exception paths.

#### WhatsAppAnalytics-11 · [MEDIUM] Identical SafeGet helper duplicated in two services

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Services/OnboardingInsightService.cs` (201-211)
- **Boyut:** duplication · **Fix-risk:** low · behavior-preserving
- **Sorun:** The private static `SafeGet<T>(Func<Task<T>> factory)` method is copy-pasted verbatim in both `RiDashboardService` (lines 96-106) and `OnboardingInsightService` (lines 201-211). Both have the same signature, same bare-catch body, and the same latent cancellation bug. A fix in one must be manually replicated in the other.
- **Önerilen fix:** Extract to a shared static helper class (e.g., `InsightHelpers.SafeGetAsync<T>`) in the same service project and reference it from both call sites. Fix the cancellation swallow (see finding above) at the same time.

#### WhatsAppAnalytics-12 · [MEDIUM] BulkOrchestrationService catches OperationCanceledException as a sector error and continues the loop

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Services/Insights/BulkOrchestrationService.cs` (53-67)
- **Boyut:** error-handling · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR · _(orijinal sev: high → düzeltildi)_
- **Sorun:** The per-sector `catch (Exception ex)` at line 64 catches all exceptions including `OperationCanceledException`. When the caller cancels the token, the cancellation exception is logged as `result.Errors.Add("sector: OperationCanceledException")` and the loop continues trying the next sector (line 52 re-checks `ct.ThrowIfCancellationRequested()`, but not before entering the catch). With N sectors this produces N error entries in `BulkMineResult.Errors` and the method returns a partially-mined result rather than honouring the cancellation.
- **Önerilen fix:** Replace `catch (Exception ex)` with `catch (Exception ex) when (ex is not OperationCanceledException)`. The `ct.ThrowIfCancellationRequested()` on line 52 then correctly exits the loop on the next iteration.

#### WhatsAppAnalytics-13 · [MEDIUM] Null-forgiving operator on ExecuteScalarAsync return value in CreateAnalysisAsync and ListAnalysesAsync

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Data/AnalyticsRepository.cs` (65, 94)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** Line 65: `return (int)result!` — if the INSERT RETURNING unexpectedly returns null (e.g., the row is filtered by a trigger or RLS), the null-forgiving `!` suppresses the compiler warning and the code throws an untyped `NullReferenceException` rather than a clear `InvalidOperationException`. Line 94: `var total = (int)(long)(await countCmd.ExecuteScalarAsync(ct))!` — same pattern on `COUNT(*)` result. Both use the null-forgiving operator which is a hard-fail pattern in this repo. `COUNT(*)` cannot return NULL but the pattern is still forbidden.
- **Önerilen fix:** Line 65: `if (result is not int id) throw new InvalidOperationException("INSERT wa_analyses returned no ID"); return id;`. Line 94: `var raw = await countCmd.ExecuteScalarAsync(ct); var total = raw is long l ? (int)l : 0;`

#### WhatsAppAnalytics-14 · [MEDIUM] AnalyticsRepository.CheckConnectionAsync uses broad catch(Exception) — swallows OperationCanceledException on health check

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Data/AnalyticsRepository.cs` (41-45)
- **Boyut:** error-handling · **Fix-risk:** trivial · ⚠️ DAVRANIŞ DEĞİŞTİRİR · _(orijinal sev: low → düzeltildi)_
- **Sorun:** The health check catches all exceptions including `OperationCanceledException` and returns `false`. If the health endpoint is called with a CT that gets cancelled (e.g. server shutting down), the method returns `false` instead of propagating the cancellation, potentially causing the health endpoint to respond 200/unhealthy rather than aborting.
- **Önerilen fix:** Change to `catch (Exception ex) when (ex is not OperationCanceledException)` to allow cancellation to propagate while still catching genuine DB connectivity failures.

#### WhatsAppAnalytics-15 · [LOW] Duplicate ClaudeClient instantiation — non-keyed singleton and two keyed singletons share no instance

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Program.cs` (129-131,175-179)
- **Boyut:** duplication · **Fix-risk:** low · behavior-preserving
- **Sorun:** A non-keyed `ClaudeClient` singleton is registered at lines 129-131 (used by Phase B NLP services: IntentClassifierService, FaqExtractorService, SentimentAnalyzerService, ProductAnalyzerService). Then two keyed `ILlmClient` singletons named `claude_haiku` and `claude_sonnet` are registered at lines 175-179, each creating a new `ClaudeClient` instance. The haiku instance at line 176 duplicates the non-keyed instance at line 129 with the same model and key — they each hold a separate `HttpClient` and lifecycle. This doubles the idle HTTP connection pool overhead for claude-haiku.
- **Kanıt:** `builder.Services.AddSingleton<ClaudeClient>(sp => new ClaudeClient(claudeApiKey, claudeModel, ...)) at line 129; builder.Services.AddKeyedSingleton<ILlmClient>("claude_haiku", (sp,_) => new ClaudeClient(claudeApiKey, claudeModel, ...)) at line 176`
- **Önerilen fix:** Register the non-keyed `ClaudeClient` first, then resolve it inside the keyed factory: `builder.Services.AddKeyedSingleton<ILlmClient>("claude_haiku", (sp, _) => sp.GetRequiredService<ClaudeClient>())`. The non-keyed registration stays for Phase B NLP services; the keyed haiku entry becomes an alias. The sonnet key still creates its own instance (different model). This reduces instantiation to two ClaudeClient instances instead of three.

#### WhatsAppAnalytics-16 · [LOW] Null-forgiving operator (!) on ExecuteScalarAsync result in CreateAnalysisAsync — hard fail

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Data/AnalyticsRepository.cs` (64-65)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** ExecuteScalarAsync returns object?. The null-forgiving operator result! is a hard fail per repo policy. If RETURNING id somehow yields no row (e.g. a trigger cancels the insert or a race condition), this throws NullReferenceException with no INV-XXX error code and no useful log context.
- **Kanıt:** `var result = await cmd.ExecuteScalarAsync(ct); return (int)result!;`
- **Önerilen fix:** var result = await cmd.ExecuteScalarAsync(ct); if (result is not int id) throw new InvalidOperationException("INV-500: CreateAnalysisAsync RETURNING id returned no value"); return id;

#### WhatsAppAnalytics-17 · [LOW] NpgsqlCommand not disposed on exception path in TemplateRepository batch upsert methods

- **Dosya:** `src/Invekto.WhatsAppAnalytics/Data/TemplateRepository.cs` (51-86, 151-172, 234-265, 328-360, 417-451, 509-540)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** In batch upsert methods (e.g., `UpsertIntentsAsync`, `UpsertFaqsAsync`, etc.), the command is created via `var cmd = conn.CreateCommand()` (no `await using`) and disposed via an explicit `await cmd.DisposeAsync()` on the happy path. If `ExecuteNonQueryAsync` throws, `cmd` is never disposed. The connection is `await using`, so the connection finalizer eventually cleans up, but this leaks the command's unmanaged resources until GC.
- **Önerilen fix:** Replace `var cmd = conn.CreateCommand()` + manual `await cmd.DisposeAsync()` with `await using var cmd = conn.CreateCommand()` in all batch methods that currently use the manual pattern. The `await using` ensures disposal on both happy path and exception path.

### Knowledge (16)

#### Knowledge-1 · [CRITICAL] GetPublishedForComparisonAsync omits group_tag — IndexOutOfRangeException at runtime

- **Dosya:** `src/Invekto.Knowledge/Data/TemplateRepository.cs` (787-801)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** GetPublishedForComparisonAsync uses a hardcoded SELECT list that enumerates exactly 21 columns (id … updated_at, indices 0–20) and does not include group_tag. It then passes the reader to ReadCatalogDto (line 801), which unconditionally accesses r.IsDBNull(21) and r.GetString(21) at lines 913–914. Accessing ordinal 21 on a 21-column result set throws IndexOutOfRangeException. Every call from TemplateExtractorService (lines 51–52 of that file) will crash. The rest of the catalog queries use the CatalogSelectColumns constant which correctly includes group_tag at index 21, so this is an isolated inconsistency introduced when group_tag was added via FEAT-WTP.
- **Kanıt:** `SELECT at lines 788–792 ends at 'updated_at' (21 columns). ReadCatalogDto at line 913: 'GroupTag = r.IsDBNull(21) ? null : r.GetString(21)'. CatalogSelectColumns at line 29 includes 'group_tag' as the 22nd column. TemplateExtractorService.cs lines 51–52 calls GetPublishedForComparisonAsync.`
- **Önerilen fix:** Replace the hardcoded column list in GetPublishedForComparisonAsync with the CatalogSelectColumns constant, identical to every other catalog-reading query in the file:

cmd.CommandText = $@"SELECT {CatalogSelectColumns} FROM template_catalog {where} ORDER BY usage_count DESC";

#### Knowledge-2 · [HIGH] Broad catch(Exception) on DB-calling request paths — hard-fail pattern

- **Dosya:** `src/Invekto.Knowledge/Program.cs` (293-297, 327-330, 390-394, 454-458, 481-485, 686-689, 710-713, 752-756)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** Eight request-path endpoint handlers call KnowledgeRepository methods (which execute Npgsql SQL) under a bare catch(Exception) instead of typed catch(NpgsqlException). This is the repo's hard-fail pattern. Affected endpoints: POST /search (line 293), GET /faqs (line 327), POST /faqs (line 390), PUT /faqs/{faqId} (line 454), DELETE /faqs/{faqId} (line 481), GET /documents (line 686), GET /documents/{docId} (line 710), DELETE /documents/{docId} (line 752). Each swallows any exception type — including OperationCanceledException, OutOfMemoryException, or ThreadAbortException — as a 500 with a wrong or generic INV error code, hiding the real failure class.
- **Kanıt:** `Line 293: catch (Exception ex) { return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeSearchFailed, ...) } — wraps retrievalService.SearchAsync which calls NpgsqlConnection. Line 327: same pattern over repo.ListFaqsAsync. Lines 481-485: repo.DeleteFaqAsync under catch(Exception) mapped to Kn…`
- **Önerilen fix:** Split each handler into typed catches: catch(NpgsqlException ex) { log + INV DB error code + 500 } catch(OperationCanceledException) { 499/408 } — leave no bare catch(Exception) on the request path. Example for /search: catch (NpgsqlException ex) { jsonLogger.StepError($"[{ErrorCodes.KnowledgeSearchFailed}] DB error: {ex.Message}", requestId); return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeSearchFailed, "Search failed", requestId), statusCode: 500); } — then let unrecognised exceptions propagate to the framework (which already returns 500 and logs via ASP.NET pipeline).

#### Knowledge-3 · [HIGH] Hardcoded Sector = "eticaret" when approving suggestion of type 'new' — silently misclassifies non-eticaret tenants

- **Dosya:** `src/Invekto.Knowledge/Program.cs` (1306-1318, 1369-1381)
- **Boyut:** correctness · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** Both the single suggestion review endpoint (line 1313) and the bulk-review endpoint (line 1375) hardcode Sector = "eticaret" when creating a new template from an approved suggestion. The TemplateSuggestionDto has no SuggestedSector field (confirmed from TemplateRepository.cs ReadSuggestionDto, lines 917-938), so the sector is never stored in the suggestion. Any suggestion raised for a dis_klinik or estetik tenant that gets approved will be inserted into the catalog with the wrong sector, making it invisible to the correct resolution path.
- **Kanıt:** `Line 1313: Sector = "eticaret", — inside the block for suggestion.SuggestionType == "new". Line 1375: same. TemplateRepository.cs ReadSuggestionDto (lines 917-938) shows no column position for sector in the suggestion SELECT.`
- **Önerilen fix:** Either: (a) add a sector field to template_suggestions table + TemplateSuggestionDto + propagate it from TemplateExtractorService (which has the analysis tenant context), then use suggestion.SuggestedSector ?? "eticaret" here; or (b) expose sector as a required field in TemplateSuggestionReviewRequest.body so the reviewer explicitly provides it before approving, with a 400 guard if missing. Option (b) is lower-risk (no migration needed) but requires UI change.

#### Knowledge-4 · [HIGH] COUNT(*) result read with GetInt32 — InvalidCastException at runtime

- **Dosya:** `src/Invekto.Knowledge/Services/TemplateExtractorService.cs` (318-334)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** ReadIntentDistributionAsync queries `SELECT intent, COUNT(*) as cnt, ...` and reads the aggregate with `r.GetInt32(1)` (line 334). PostgreSQL returns COUNT(*) as bigint (int8), not int4. GetInt32 on a bigint column throws InvalidCastException at runtime when any result row is present. The query is executed on every template-extraction run, so this is a guaranteed crash path whenever the wa_intents table has rows.
- **Kanıt:** `cmd.CommandText = @"SELECT intent, COUNT(*) as cnt, ... GROUP BY intent"; ... MessageCount = r.GetInt32(1)`
- **Önerilen fix:** Cast in SQL: `COUNT(*)::int as cnt` so PostgreSQL returns int4, or read with `r.GetInt64(1)` and assign to a long field. The simplest safe fix with no schema change is `COUNT(*)::int`.

#### Knowledge-5 · [MEDIUM] Template catalog endpoints make bare unhandled DB calls — no try/catch at all

- **Dosya:** `src/Invekto.Knowledge/Program.cs` (1052-1053, 1066-1071, 1172-1178, 1193-1198, 1212-1217, 1230-1231, 1251-1252, 1265-1269, 1621-1622)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** Nine endpoint handlers call TemplateRepository or KnowledgeRepository methods with zero try/catch: GET /catalog (line 1052), GET /catalog/{id} (line 1066), PUT /catalog/{id} (line 1172), DELETE /catalog/{id} (line 1193), POST /catalog/{id}/publish (line 1212), GET /catalog/{id}/versions (line 1230), GET /suggestions (line 1251), GET /suggestions/{id} (line 1265), GET /{tenantId}/adoptions (line 1621). Any NpgsqlException will bubble up to ASP.NET's default exception handler, producing a raw framework 500 with no INV-XXX code in the response and no structured log entry via JsonLinesLogger.
- **Kanıt:** `Line 1052: var (items, total) = await repo.ListAsync(filter); — no surrounding try/catch. Line 1193: var deleted = await repo.SoftDeleteAsync(id); — no surrounding try/catch. These are superadmin-only paths but they share the same production Postgres instance.`
- **Önerilen fix:** Wrap each repo call in catch(NpgsqlException ex) returning the appropriate INV error code (e.g., TemplateNotFound / TemplateComparisonFailed class codes) with jsonLogger.StepError. The fix is mechanical and identical across all nine endpoints.

#### Knowledge-6 · [MEDIUM] Template create (POST /catalog) only catches PostgresException 23505, leaving all other NpgsqlException unhandled

- **Dosya:** `src/Invekto.Knowledge/Program.cs` (1139-1150)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The POST /templates/catalog handler catches only Npgsql.PostgresException with SqlState 23505 (unique violation). Any other NpgsqlException — connection timeout, constraint violation from a different column, serialization failure — propagates unhandled. The repo's hard-fail rule says DB catch must use NpgsqlException as the base, not only PostgresException.
- **Kanıt:** `Line 1146: catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505") { ... } — no base NpgsqlException fallback catch after it.`
- **Önerilen fix:** Add a second catch block after the 23505 guard: catch(NpgsqlException ex) { jsonLogger.StepError($"[{ErrorCodes.KnowledgeImportDbError}] Template create DB error: {ex.Message}", requestId); return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeImportDbError, "Template create failed", requestId), statusCode: 500); }

#### Knowledge-7 · [MEDIUM] Document upload broad catch(Exception) swallows NpgsqlException from InsertDocumentAsync as KnowledgeUploadFailed

- **Dosya:** `src/Invekto.Knowledge/Program.cs` (531-581)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** The document upload handler wraps the entire block — including repo.GetTenantHealthInfoAsync (line 549), repo.InsertDocumentAsync (line 557), and the file save — in a single catch(Exception) that maps all failures to KnowledgeUploadFailed (line 580). A DB connection failure during InsertDocumentAsync is indistinguishable from a file-write error. Additionally the file cleanup logic (lines 573-578) runs for DB failures too, which is correct for orphan prevention, but a DB failure is mapped identically to an IO failure, hindering diagnosis.
- **Kanıt:** `Line 571: catch (Exception ex) { ... return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeUploadFailed, "File upload failed", requestId), statusCode: 500); } — reached by any NpgsqlException from line 549 or 557.`
- **Önerilen fix:** Split: catch(NpgsqlException ex) → KnowledgeImportDbError + log + cleanup + 500; catch(IOException ex) → KnowledgeUploadFailed + log + cleanup + 500. The orphan-file cleanup should run in both branches.

#### Knowledge-8 · [MEDIUM] UpdateAsync reads version then updates without a transaction — silent version record loss under concurrency

- **Dosya:** `src/Invekto.Knowledge/Data/TemplateRepository.cs` (156-206)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving
- **Sorun:** UpdateAsync first executes a SELECT to read the current version (line 163–164), computes newVersion = current + 1 (line 167), then runs the UPDATE (line 189) and InsertVersionInternalAsync (line 204) — all on the same connection but with no enclosing transaction. Under concurrent updates from two callers: both reads return version N, both compute N+1, both UPDATE succeed (last writer wins on the catalog row), but InsertVersionInternalAsync uses ON CONFLICT (template_id, version) DO NOTHING — so the second insert is silently dropped and that update has no version history record. Additionally the version column on the catalog row reflects whichever UPDATE ran last, potentially losing an intermediate state. This is a data-integrity issue in the audit trail rather than a crash.
- **Kanıt:** `Line 163: 'getCmd.CommandText = "SELECT version FROM template_catalog WHERE id = @id AND is_active = true"' — no BEGIN TRANSACTION before or after. Line 189: 'cmd.CommandText = $"UPDATE template_catalog SET {string.Join(", ", sets)} WHERE id = @id AND is_active = true"'. Line 849 in InsertVersionInt…`
- **Önerilen fix:** Wrap the entire UpdateAsync body in a serializable (or at minimum repeatable-read) transaction with FOR UPDATE on the initial SELECT:

await using var tx = await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
getCmd.Transaction = tx;
// ... existing logic ...
cmd.Transaction = tx;
// InsertVersionInternalAsync receives conn (already in tx)
await tx.CommitAsync(ct);

This ensures the version read and the subsequent write are atomic. Pass the transaction object through to InsertVersionInternalAsync (add an optional NpgsqlTransaction? parameter).

#### Knowledge-9 · [MEDIUM] Broad catch(Exception) in RetrievalService semantic-search fallback — swallows OperationCanceledException

- **Dosya:** `src/Invekto.Knowledge/Services/RetrievalService.cs` (58-61)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** The semantic search try-block catches `Exception ex` without a `when(!ct.IsCancellationRequested)` guard (line 58). If the caller cancels the request, OperationCanceledException is caught, logged as 'Semantic search failed', and execution falls through to keyword search instead of propagating the cancellation. This means cancelled requests continue processing, wasting resources and potentially returning stale results. Repo rule: broad catch(Exception) is forbidden.
- **Kanıt:** `catch (Exception ex) { _logger.SystemWarn($"RetrievalService: Semantic search failed ({ex.Message}), falling back to keyword search"); }`
- **Önerilen fix:** Replace with: `catch (OperationCanceledException) { throw; } catch (NpgsqlException ex) { _logger.SystemWarn(...); } catch (HttpRequestException ex) { _logger.SystemWarn(...); }` — match the typed exceptions that SemanticSearch can actually throw.

#### Knowledge-10 · [LOW] generate-embeddings broad catch misattributes NpgsqlException as KnowledgeOpenAIError

- **Dosya:** `src/Invekto.Knowledge/Program.cs` (964-1022)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** The POST /generate-embeddings handler wraps both DB calls (repo.GetFaqsWithoutEmbeddingAsync, repo.UpdateFaqEmbeddingAsync, repo.GetChunksWithoutEmbeddingAsync, repo.BatchUpdateChunkEmbeddingsAsync) and OpenAI HTTP calls (embeddingService.GetEmbeddingAsync) inside a single catch(Exception) that maps everything to ErrorCodes.KnowledgeOpenAIError (line 1020). A Postgres connection failure will surface as 'OpenAI error' in logs and to callers, making DB-layer incidents invisible under the wrong error code.
- **Kanıt:** `Line 1018-1021: catch (Exception ex) { jsonLogger.StepError($"[{ErrorCodes.KnowledgeOpenAIError}] Embedding generation failed: {ex.Message}", requestId); — this is reached by NpgsqlException thrown from line 971 (GetFaqsWithoutEmbeddingAsync) or line 978 (UpdateFaqEmbeddingAsync).`
- **Önerilen fix:** Split: catch(NpgsqlException ex) → KnowledgeImportDbError + 500; catch(HttpRequestException ex) → KnowledgeOpenAIError + 502; let OperationCanceledException propagate. Individual embedding call failures are already swallowed per-item (lines 381-385, 445-449) so the outer catch only needs to cover the initial DB fetch calls.

#### Knowledge-11 · [LOW] Duplication: fire-and-forget embedding Task.Run pattern repeated identically in Create FAQ and Update FAQ

- **Dosya:** `src/Invekto.Knowledge/Program.cs` (371-386, 431-450)
- **Boyut:** duplication · **Fix-risk:** low · behavior-preserving
- **Sorun:** The fire-and-forget embedding generation block (Task.Run → GetEmbeddingAsync → UpdateFaqEmbeddingAsync → catch(Exception) log) is copy-pasted verbatim in the Create FAQ handler (lines 371-386) and the Update FAQ handler (lines 431-450), with only the faqId source differing. Any future change (retry policy, error code, timeout) must be made in both places.
- **Kanıt:** `Lines 372-386: _ = Task.Run(async () => { try { var text = $"{body.Question} {body.Answer}"; var embedding = await embeddingService.GetEmbeddingAsync(text); if (embedding != null) await repo.UpdateFaqEmbeddingAsync(tenantId, faq.Id, embedding); } catch (Exception ex) { jsonLogger.SystemWarn(...) } }…`
- **Önerilen fix:** Extract a local helper method, e.g., static void EnqueueEmbeddingUpdate(EmbeddingService svc, KnowledgeRepository repo, JsonLinesLogger log, int tenantId, long faqId, string question, string answer), and call it from both endpoints. Behavior is identical.

#### Knowledge-12 · [LOW] Broad catch(Exception) in DocumentProcessingService.StartAsync — hides startup NpgsqlException

- **Dosya:** `src/Invekto.Knowledge/Services/DocumentProcessingService.cs` (65-68)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** StartAsync catches all exceptions from GetStuckDocumentsAsync with a generic `catch (Exception ex)`. If the DB is unreachable on startup (NpgsqlException), it is silently swallowed and the service continues without re-queuing stuck documents. The repo rule forbids broad catch without an INV-xxx code. This also hides OperationCanceledException if the host is shutting down during startup.
- **Kanıt:** `catch (Exception ex) { _logger.SystemWarn($"[DocumentProcessingService] Failed to recover stuck documents on startup: {ex.Message}"); }`
- **Önerilen fix:** Use `catch (NpgsqlException ex) { _logger.SystemWarn($"[INV-5xx] ..."); }` and let OperationCanceledException propagate. If a true fallback is required, add `catch (Exception ex) when (ex is not OperationCanceledException)`.

#### Knowledge-13 · [LOW] Broad catch(Exception) in DocumentProcessingService.ExecuteAsync worker loop — swallows OperationCanceledException

- **Dosya:** `src/Invekto.Knowledge/Services/DocumentProcessingService.cs` (94-106)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** The background worker's per-job try-block (line 94) catches Exception without excluding OperationCanceledException. When the host cancels stoppingToken, the inner ProcessDocumentAsync throws OperationCanceledException, it is caught here, an attempt is made to update the DB status (with the already-cancelled token), and the inner DB catch (line 102) catches another Exception. The service loop then continues to the next iteration, calling _signal.WaitAsync(stoppingToken) which immediately throws again — causing a loud log storm before breaking. Additionally, the inner catch at line 102 is another broad Exception.
- **Kanıt:** `catch (Exception ex) { _logger.SystemError(...); try { await _repository.UpdateDocumentStatusAsync(..., stoppingToken); } catch (Exception dbEx) { ... } }`
- **Önerilen fix:** Add `when (ex is not OperationCanceledException)` to the outer catch, or rethrow OperationCanceledException. The inner DB catch should use `catch (NpgsqlException dbEx)`. If cancellation hits mid-document, let the loop exit cleanly rather than logging a spurious error.

#### Knowledge-14 · [LOW] EmbeddingService uses request.Headers.Add for Authorization — FormatException risk on malformed key

- **Dosya:** `src/Invekto.Knowledge/Services/EmbeddingService.cs` (73)
- **Boyut:** security · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** The Authorization header is set via `request.Headers.Add("Authorization", $"Bearer {_apiKey}")`. `Headers.Add` throws FormatException if the value contains characters that violate header syntax (e.g., if a misconfigured key contains newlines or forbidden chars). The repo pattern requires `TryAddWithoutValidation` for per-request secrets on HttpRequestMessage. Although this is a per-request header (not DefaultRequestHeaders, so no cross-tenant leak), the FormatException would not be caught by the typed HttpRequestException catch and would bubble up as an uncaught exception through the retry loop, bypassing all retry logic.
- **Kanıt:** `request.Headers.Add("Authorization", $"Bearer {_apiKey}");`
- **Önerilen fix:** Use `request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiKey}");` to match the repo pattern and avoid FormatException on malformed key values.

#### Knowledge-15 · [LOW] InferCategory duplicated verbatim across ImportService and TemplateExtractorService

- **Dosya:** `src/Invekto.Knowledge/Services/ImportService.cs` (370-390)
- **Boyut:** duplication · **Fix-risk:** low · behavior-preserving
- **Sorun:** The `InferCategory(string question)` method with identical Turkish keyword-matching logic exists as a private static method in both ImportService (line 370) and TemplateExtractorService (line 423). The two copies are independent but diverge slightly (ImportService checks 'kaç tl', 'tutar'; TemplateExtractorService checks 'kadar', 'taksit') — a maintenance trap that will cause silent inconsistency as the keyword lists evolve. Similarly, `ExtractKeywords` has separate implementations in ImportService, SeSeedService, and TemplateExtractorService with different tokenization logic.
- **Kanıt:** `ImportService.cs line 370: `private static string InferCategory(string question)` and TemplateExtractorService.cs line 423: `private static string InferCategory(string question)` — identical method names with overlapping but diverged bodies.`
- **Önerilen fix:** Move InferCategory and the Turkish keyword utilities to a shared static helper class in Invekto.Knowledge (e.g., `KnowledgeTextUtils`) and reference it from both services. This is a within-service (not cross-microservice) refactor so it does not violate isolation rules.

#### Knowledge-16 · [LOW] MapTemplateTypeToTargetType maps 'scenario' to 'faq' — incorrect target type recorded in adoption log

- **Dosya:** `src/Invekto.Knowledge/Services/TemplateAdoptionService.cs` (330-338)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving
- **Sorun:** MapTemplateTypeToTargetType maps `"scenario"` to `"faq"` (line 336) and the wildcard default arm also maps to `"faq"` (line 337). For scenario templates, AdoptAsync explicitly returns `targetId = null` (line 62) and the comment explains 'Reference only — no clone needed'. Recording target_type='faq' for a scenario adoption record is semantically wrong and will confuse any query that joins adoption records on target_type to find actual FAQ entries. The default arm mapping unknown types to 'faq' silently misclassifies any future template type.
- **Kanıt:** `"scenario" => "faq", _ => "faq"`
- **Önerilen fix:** Map `"scenario" => "scenario_ref"` and `_ => "unknown"` (or throw ArgumentOutOfRangeException for unexpected types). This ensures the adoption audit record accurately reflects that no FAQ row was cloned.

### AgentAI (9)

#### AgentAI-1 · [MEDIUM] Broad catch(Exception) wraps DB call in AgentProfileBuilder — NpgsqlException never mapped

- **Dosya:** `src/Invekto.AgentAI/Services/AgentProfileBuilder.cs` (29-35)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** GetRecentFeedbackAsync throws NpgsqlException on DB failure. The catch(Exception) block swallows it without any INV-XXX error code mapping and returns null silently. Repo rule: broad catch(Exception) is forbidden; DB errors must be caught as NpgsqlException with an error code mapped. The return null causes the /api/v1/suggest request to continue with no agent profile, which is acceptable graceful degradation, but the DB error class is invisible.
- **Kanıt:** `catch (Exception ex) { _logger.StepError($"Failed to fetch agent feedback history: {ex.Message}", "-"); return null; }`
- **Önerilen fix:** Replace with: catch (NpgsqlException ex) { _logger.StepError($"[{ErrorCodes.DatabaseQueryFailed}] Feedback history DB error: {ex.Message}", "-"); return null; } — plus a separate catch (OperationCanceledException) { throw; } guard before it.

#### AgentAI-2 · [MEDIUM] Broad catch(Exception) on DB log in /api/v1/suggest — NpgsqlException unmapped

- **Dosya:** `src/Invekto.AgentAI/Program.cs` (311-329)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** LogSuggestionAsync is a DB write. The caller catches Exception broadly with no error code. Any NpgsqlException (e.g. connection failure, constraint violation) is reported only as a generic string log. Repo rule: DB catch must use NpgsqlException. The broad catch here also silently absorbs OperationCanceledException from the passed CancellationToken.None, which cannot actually cancel but breaks the pattern for future refactors.
- **Kanıt:** `catch (Exception ex) { dbLogFailed = true; jsonLogger.StepError($"Failed to log suggestion to DB: {ex.Message}", requestId); }`
- **Önerilen fix:** catch (NpgsqlException ex) { dbLogFailed = true; jsonLogger.StepError($"[{ErrorCodes.DatabaseQueryFailed}] Suggestion DB log error: {ex.Message}", requestId); }

#### AgentAI-3 · [MEDIUM] Broad catch(Exception) on feedback DB update in /api/v1/feedback — NpgsqlException unmapped

- **Dosya:** `src/Invekto.AgentAI/Program.cs` (400-426)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** UpdateFeedbackAsync is a DB write. The catch(Exception) block maps all failures to ErrorCodes.GeneralUnknown (500) with no NpgsqlException distinction. If the error is a transient network issue versus a schema violation, callers receive identical 500 responses. Repo rule requires typed NpgsqlException catch for DB paths.
- **Kanıt:** `catch (Exception ex) { jsonLogger.StepError($"Feedback DB update failed: {ex.Message}", requestId); return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", requestId), statusCode: 500); }`
- **Önerilen fix:** catch (NpgsqlException ex) { jsonLogger.StepError($"[{ErrorCodes.DatabaseQueryFailed}] Feedback DB error: {ex.Message}", requestId); return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseQueryFailed, "Internal server error", requestId), statusCode: 500); }

#### AgentAI-4 · [MEDIUM] Broad catch(Exception) in ReplyGenerator.GenerateAsync masks non-HTTP exceptions

- **Dosya:** `src/Invekto.AgentAI/Services/ReplyGenerator.cs` (123-128)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** The outer catch(Exception) in GenerateAsync absorbs everything that is not a timeout: HttpRequestException, FormatException from Headers.Add if the API key is malformed, JsonException from ReadFromJsonAsync, InvalidOperationException from GetProperty on unexpected shape — all silently return null. The caller treats null as 'generation failed' and serves a 500, but the error class is never visible in structured logs. Repo rule forbids broad catch(Exception). Should be split into typed catches: HttpRequestException, JsonException, then a final typed rethrow for unexpected exceptions.
- **Kanıt:** `catch (Exception ex) { sw.Stop(); _logger.SystemWarn($"[ReplyGenerator] Reply generation failed after {sw.ElapsedMilliseconds}ms: {ex.Message}"); return null; }`
- **Önerilen fix:** Split into: catch (HttpRequestException ex) { ... return null; } catch (JsonException ex) { ... return null; } — let any other exception propagate so the MinimalAPI middleware handles it as a true 500 with stack trace visible in logs.

#### AgentAI-5 · [MEDIUM] Broad catch(Exception) in ReplyGenerator.ParseResponse silently swallows InvalidOperationException from JSON shape mismatches

- **Dosya:** `src/Invekto.AgentAI/Services/ReplyGenerator.cs` (279-284)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** ParseResponse already has a correctly typed catch(JsonException). The trailing catch(Exception) exists to handle GetProperty throwing InvalidOperationException when 'content' or 'text' is missing (line 96-99 in GenerateAsync, but also indirectly in ParseResponse). Broad catch is forbidden by repo rules. The actual non-JSON exceptions here are likely InvalidOperationException from JsonElement access on wrong ValueKind. Should be narrowed.
- **Kanıt:** `catch (Exception ex) { _logger.SystemWarn($"[ReplyGenerator] Failed to parse reply response: {ex.Message}, raw={responseText}"); return null; }`
- **Önerilen fix:** Replace with catch (InvalidOperationException ex) (for JsonElement.GetProperty on wrong kind) and remove the trailing broad catch; let truly unexpected exceptions propagate.

#### AgentAI-6 · [MEDIUM] Broad catch(Exception) in ConversationSummarizer.SummarizeIfNeededAsync

- **Dosya:** `src/Invekto.AgentAI/Services/ConversationSummarizer.cs` (123-127)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** Same pattern as ReplyGenerator: the trailing catch(Exception) absorbs HttpRequestException, JsonException, InvalidOperationException from JSON shape, etc. all with the same log line and graceful return. Repo rule forbids this. Failures that are genuinely unrecoverable (e.g. OOM) are silently swallowed.
- **Kanıt:** `catch (Exception ex) { _logger.SystemWarn($"[ConversationSummarizer] Summary failed: {ex.Message}, using raw history"); return (null, history); }`
- **Önerilen fix:** Split into catch (HttpRequestException), catch (JsonException), catch (InvalidOperationException); let OutOfMemoryException and similar propagate.

#### AgentAI-7 · [LOW] Broad catch(Exception) in Program.cs wrapping ConversationSummarizer call

- **Dosya:** `src/Invekto.AgentAI/Program.cs` (255-265)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** SummarizeIfNeededAsync already handles all graceful-degradation cases internally and re-throws OperationCanceledException for app shutdown. The outer catch(Exception) at the call site therefore only serves to catch exceptions that should propagate (e.g. if the internal implementation changes to throw on unexpected error). The broad catch here silently swallows any unexpected exception from the summarizer and allows the request to continue with potentially inconsistent state (recentHistory would still be the full list because the assignment on line 260 was not reached).
- **Kanıt:** `catch (Exception ex) { jsonLogger.StepWarn($"Conversation summarization failed: {ex.Message}", requestId); }`
- **Önerilen fix:** Remove the outer catch entirely; SummarizeIfNeededAsync already returns (null, history) on all expected failures. If a catch is still desired, narrow to catch (InvalidOperationException) to avoid swallowing genuine unexpected exceptions.

#### AgentAI-8 · [LOW] ParseSearchResponse enumerates the 'results' JSON array twice — duplication and wasted allocation

- **Dosya:** `src/Invekto.AgentAI/Services/KnowledgeHttpClient.cs` (106-161)
- **Boyut:** duplication · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** ParseSearchResponse calls root.TryGetProperty("results", ...) twice (lines 106 and 140) and runs two separate foreach loops over the same array: the first to build the KnowledgeSourceRef list (metadata only), the second to build the context text strings. This doubles the JSON element traversal and the two loops contain duplicated sourceType checks and property reads. With topK=5 the impact is negligible, but the duplication is a maintenance burden.
- **Kanıt:** `if (root.TryGetProperty("results", out var resultsArr)) { foreach (var item in resultsArr.EnumerateArray()) { ... } } — then again — if (root.TryGetProperty("results", out var ra)) { foreach (var item in ra.EnumerateArray()) { ... } }`
- **Önerilen fix:** Merge both loops into one: iterate once, build both the KnowledgeSourceRef entry and the context string in the same pass. Remove the second TryGetProperty call.

#### AgentAI-9 · [LOW] HttpRequestMessage.Headers.Add used for x-api-key instead of TryAddWithoutValidation — potential FormatException on malformed key

- **Dosya:** `src/Invekto.AgentAI/Services/ReplyGenerator.cs` (75-76)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** httpRequest.Headers.Add("x-api-key", _apiKey) validates the header value and throws FormatException if the value contains invalid characters (e.g. newline, carriage return). In production the API key is read from config at startup and is unlikely to be malformed, but it is inconsistent with the repo-wide pattern (TryAddWithoutValidation is used everywhere else across all services for per-request headers). The same issue appears in ConversationSummarizer.cs lines 86-87. These are per-request HttpRequestMessage headers so there is no cross-tenant leak risk, but a FormatException here would be swallowed by the broad catch(Exception) on line 123, turning it into a silent 'generation failed' with a confusing log message.
- **Kanıt:** `httpRequest.Headers.Add("x-api-key", _apiKey); httpRequest.Headers.Add("anthropic-version", "2023-06-01"); — ReplyGenerator.cs:75-76; same at ConversationSummarizer.cs:86-87`
- **Önerilen fix:** Replace with httpRequest.Headers.TryAddWithoutValidation("x-api-key", _apiKey); to match repo pattern and eliminate the silent FormatException failure mode. Apply identically in ConversationSummarizer.cs:86-87.

### Appointments (8)

#### Appointments-1 · [CRITICAL] GetInt32 on COUNT(*) bigint in GetAvailableSlotsAsync throws InvalidCastException

- **Dosya:** `src/Invekto.Appointments/Data/AppointmentsRepository.cs` (670-700)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The COALESCE(COUNT(*), 0) subquery at line 673 returns bigint in PostgreSQL. The reader call at line 700 uses reader.GetInt32(5), which throws InvalidCastException at runtime because Npgsql maps bigint to Int64, not Int32. Every call to GetAvailableSlotsAsync will crash on any slot that has at least one confirmed booking (the COALESCE(0) literal is also typed as bigint in the subquery result). The GetNoShowStatsAsync at lines 641-642 handles this correctly with GetInt64 — this inconsistency is the exact pattern the project hard-fail rules flag.
- **Kanıt:** `CurrentBookings = reader.GetInt32(5)  // col 5 = COALESCE(COUNT(*), 0) — bigint`
- **Önerilen fix:** Cast in SQL: COALESCE((SELECT COUNT(*)::int FROM ...), 0) AS current_bookings — then GetInt32(5) is safe. Alternatively keep the SQL as-is and change to CurrentBookings = (int)reader.GetInt64(5) to match the GetNoShowStatsAsync pattern.

#### Appointments-2 · [HIGH] Null-forgiving operator on ExecuteScalarAsync result in BookAppointmentAsync

- **Dosya:** `src/Invekto.Appointments/Data/AppointmentsRepository.cs` (194-195)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** Line 195 casts ExecuteScalarAsync result with (long)result! — the null-forgiving operator (!) is a repo hard-fail pattern. ExecuteScalarAsync returns null when no row is returned (e.g. a BEFORE INSERT trigger aborts the INSERT, or the connection is interrupted mid-flight after execution). The null-forgiving cast produces NullReferenceException with no INV-XXX error code and no useful context logged. The same pattern is used safely in CreateSlotAsync (line 87) and CreatePricingAsync (line 580) with a null-safe Convert.ToInt32, but BookAppointmentAsync skips that guard and directly dereferences.
- **Kanıt:** `var result = await cmd.ExecuteScalarAsync(ct); ⏎ return (long)result!;`
- **Önerilen fix:** if (result is null) throw new InvalidOperationException("BookAppointmentAsync: INSERT RETURNING id returned null");
return result is long id ? id : Convert.ToInt64(result); — mirrors the safe pattern used in CreateSlotAsync at line 87.

#### Appointments-3 · [HIGH] Check-then-act TOCA race: slot capacity check and INSERT are on separate connections with no serialization

- **Dosya:** `src/Invekto.Appointments/Program.cs` (439-456)
- **Boyut:** correctness · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** The booking flow reads CountConfirmedForSlotAsync (opens and closes connection A) then calls BookAppointmentAsync (opens connection B and INSERTs). No transaction, no advisory lock, no DB-level constraint ties these two steps together. Under concurrent booking requests for the same slot on the same date (plausible in a clinic with a popular slot), N concurrent requests can all read confirmedCount < maxBookings before any INSERT commits and all proceed to book, producing overbooking by N-1. The fix must be in the repository layer — application-level check-then-act cannot be made safe across separate connections.
- **Kanıt:** `// Program.cs 439: var confirmedCount = await repository.CountConfirmedForSlotAsync(...) ⏎ // Program.cs 453: var id = await repository.BookAppointmentAsync(...) — separate connection, no lock`
- **Önerilen fix:** Replace the two-step flow with a single CTE in BookAppointmentAsync that atomically checks capacity and inserts: WITH capacity_check AS (SELECT COUNT(*) < s.max_bookings AS has_space FROM appointments JOIN appointment_slots s ON s.id = @slotId WHERE appointment_date = @date AND slot_id = @slotId AND status = 'confirmed'), ins AS (INSERT INTO appointments (...) SELECT ... WHERE (SELECT has_space FROM capacity_check) RETURNING id) SELECT id FROM ins. If id IS NULL, the slot was full at insert time — return a typed error. Alternatively add a DB-level partial unique index or a trigger that enforces the max_bookings constraint, so any race produces a constraint violation (caught as NpgsqlException) rather than silent overbooking.

#### Appointments-4 · [HIGH] COUNT(*) subquery in GetAvailableSlotsAsync read via GetInt32 — runtime InvalidCastException

- **Dosya:** `src/Invekto.Appointments/Data/AppointmentsRepository.cs` (673-700)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The SQL subquery `COALESCE((SELECT COUNT(*) FROM appointments a ...), 0) AS current_bookings` produces a `bigint` column — PostgreSQL COUNT(*) always returns bigint, and COALESCE preserves the dominant type. Line 700 reads this column with `reader.GetInt32(5)`, which Npgsql rejects at runtime with an InvalidCastException because the wire type is int8, not int4. This means every call to GET /api/v1/appointments/available-slots will throw as soon as any slot has at least one booking (or even with zero, since COALESCE(bigint, 0::int) still gives bigint).
- **Kanıt:** `Line 673: `(SELECT COUNT(*) FROM appointments a ...)`; Line 700: `CurrentBookings = reader.GetInt32(5)``
- **Önerilen fix:** Cast the subquery result in SQL: `COALESCE((SELECT COUNT(*) FROM appointments a ...), 0)::int AS current_bookings`. Alternatively change the reader call to `(int)reader.GetInt64(5)`. The SQL cast is cleaner and makes the intent explicit at the query layer.

#### Appointments-5 · [MEDIUM] POST /api/v1/pricing catches only PostgresException for unique violation — other DB errors propagate unmapped

- **Dosya:** `src/Invekto.Appointments/Program.cs` (765-776)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The pricing creation endpoint wraps `repository.CreatePricingAsync` in a try block that only catches `Npgsql.PostgresException when (ex.SqlState == "23505")`. Any other DB-level failure — connection loss, NpgsqlException from the pool, schema mismatch — propagates unhandled outside this try and becomes an unformatted 500 with no INV error code. Every other endpoint in this file either uses a dedicated NpgsqlException catch or bubbles through the framework. The repo rule says DB catch must catch NpgsqlException (base), not only PostgresException. The specific PostgresException when-guard is correct for unique violation handling but an outer NpgsqlException fallback is missing.
- **Kanıt:** `Lines 765-776: `try { var id = await repository.CreatePricingAsync(...) ... } catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505") { ... }` — no enclosing NpgsqlException catch.`
- **Önerilen fix:** Add an outer `catch (Npgsql.NpgsqlException ex)` after the inner PostgresException catch that returns `Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Pricing creation failed due to a database error", rid), statusCode: 500)` with a SystemError log, matching the pattern used by the lifecycle endpoints on lines 879-882 of Program.cs.

#### Appointments-6 · [LOW] LOCALTIME in GetPending2hRemindersAsync compares against PostgreSQL server local time, not tenant clinic timezone

- **Dosya:** `src/Invekto.Appointments/Data/AppointmentsRepository.cs` (325-340)
- **Boyut:** correctness · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** Lines 335-336 compare a.start_time (a TIME WITHOUT TIME ZONE column) against LOCALTIME and LOCALTIME + INTERVAL '2 hours'. LOCALTIME returns the PostgreSQL server's local time, which is UTC on most Linux/Windows server deployments. If a tenant's clinic is in Europe/Istanbul (UTC+3), the 10:00 appointment slot will be matched only when the server clock reads 10:00 UTC — i.e., at 13:00 Istanbul time, one hour after the appointment has already started. The result is either missed reminders or reminders firing at the wrong time. The GetPending48hRemindersAsync uses CURRENT_DATE which has the same implicit server-timezone dependency but is less acute. GetTenantTimezoneAsync already exists in the repository (line 1241) but is not used in the reminder queries.
- **Kanıt:** `AND a.start_time > LOCALTIME ⏎ AND a.start_time <= LOCALTIME + INTERVAL '2 hours'`
- **Önerilen fix:** Parameterise the current time from the application layer using DateTimeOffset.UtcNow converted to the tenant timezone, or use NOW() AT TIME ZONE 'UTC' consistently and store/compare appointment times in UTC. Since start_time is currently stored as TIME (naive), the safest short-term fix is: pass the server-side window as parameters from C# using DateTime.Now (server time) and ensure the production server timezone matches clinic timezone, documenting the constraint. Long-term: store appointment times with timezone or as UTC epoch and convert at query time using the per-tenant timezone from tenant_settings.

#### Appointments-7 · [LOW] ValidLifecycleTypes duplicated between Program.cs and LifecycleStepDefinitions — divergence risk on new type addition

- **Dosya:** `src/Invekto.Appointments/Program.cs` (855)
- **Boyut:** duplication · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** Program.cs line 855 declares `var validTypes = new[] { "post_treatment", "plan_approval", "pre_op" }` inline for the /lifecycle/start validation. The identical set is already defined as `LifecycleStepDefinitions.ValidLifecycleTypes` (LifecycleStepDefinitions.cs line 10-13). If a new lifecycle type is added to `LifecycleStepDefinitions` (step switch + static list), the developer must remember to also update the inline array in Program.cs or the API will reject the new type with 400 while the step engine would accept it. The duplicate is the single authoritative validation gap.
- **Kanıt:** `Program.cs line 855: `var validTypes = new[] { "post_treatment", "plan_approval", "pre_op" }` vs LifecycleStepDefinitions.cs line 10-13: `public static readonly IReadOnlyList<string> ValidLifecycleTypes = new[] { "post_treatment", "plan_approval", "pre_op" }``
- **Önerilen fix:** Replace the inline array with `LifecycleStepDefinitions.ValidLifecycleTypes`: `if (!LifecycleStepDefinitions.ValidLifecycleTypes.Contains(request.LifecycleType))`. No behavior change; the error message can stay the same using `string.Join(", ", LifecycleStepDefinitions.ValidLifecycleTypes)`.

#### Appointments-8 · [LOW] Template resolution logic duplicated inline in StartLifecycleAsync instead of calling ResolveTemplate

- **Dosya:** `src/Invekto.Appointments/Services/TreatmentLifecycleService.cs` (293-295)
- **Boyut:** duplication · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** Lines 293-295 in `StartLifecycleAsync` repeat the exact same two `.Replace()` calls that are already extracted into the `private static ResolveTemplate(string template, DueStepCandidate candidate)` helper (lines 251-256). They are currently identical in output, but if a third template variable is added to `ResolveTemplate` (e.g. `{{step_key}}`), the inline version in `StartLifecycleAsync` will silently skip it, producing different templates for step creation vs step dispatch.
- **Kanıt:** `Lines 293-295: `.Replace("{{patient_name}}", patientName).Replace("{{treatment_type}}", request.TreatmentType ?? "tedavi")` vs lines 253-255: `template.Replace("{{patient_name}}", candidate.PatientName).Replace("{{treatment_type}}", candidate.TreatmentType ?? "tedavi")``
- **Önerilen fix:** Extract template resolution from `StartLifecycleAsync` to use a shared static helper. Since `StartLifecycleAsync` works with raw strings rather than a `DueStepCandidate`, either overload `ResolveTemplate` to accept `(string template, string patientName, string? treatmentType)`, or extract a `ResolveTemplate(string template, string patientName, string? treatmentType)` overload and have both call sites use it.

### Integrations (6)

#### Integrations-1 · [CRITICAL] COUNT(*) bigint read via GetInt32 throws InvalidCastException at runtime

- **Dosya:** `src/Invekto.Integrations/Data/IntegrationsRepository.cs` (562-580)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** GetReviewRecoveryStatsAsync executes 'SELECT recovery_status, provider, COUNT(*) as cnt' and reads the count column with reader.GetInt32(2). PostgreSQL COUNT(*) always returns bigint (int64), not int (int32). GetInt32 on a bigint column throws System.InvalidCastException at runtime. This means any call to GetReviewRecoveryStatsAsync will crash, making the review recovery stats endpoint entirely broken.
- **Kanıt:** `Line 562: 'COUNT(*) as cnt' — PostgreSQL returns bigint. Line 580: 'var count = reader.GetInt32(2);' — GetInt32 requires int4, receives int8, throws InvalidCastException.`
- **Önerilen fix:** Change the SQL to cast: 'COUNT(*)::int as cnt' and keep GetInt32(2), OR change the reader call to reader.GetInt64(2) and adjust the accumulator type to long. The SQL cast is cleaner: it is behavior-preserving (review_alerts row counts will never exceed int32 range) and is the documented repo pattern.

#### Integrations-2 · [HIGH] Duplicate DI registration shadows AddHttpClient timeout config — configured timeouts are dead code

- **Dosya:** `src/Invekto.Integrations/Program.cs` (89-98)
- **Boyut:** correctness · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** AddHttpClient<IkasTokenManager> and AddHttpClient<IkasGraphQlClient> each configure a named HttpClient with an explicit Timeout (10s and 15s). These calls also register the typed client as a transient that receives the named HttpClient from IHttpClientFactory. Immediately after, AddSingleton<IkasTokenManager>() and AddSingleton<IkasGraphQlClient>() add a second, independent registration for the same type. In .NET DI the last registration wins for direct resolution. When IkasProvider (also singleton) receives its IkasTokenManager / IkasGraphQlClient constructor arguments, the container resolves via the singleton registration — which constructs the type with an HttpClient injected from the default (unnamed) pool, NOT the one configured with the 10s/15s timeout. The timeout lines (91, 96) are effectively dead code: they configure a named client that is never used. In production this means ikas OAuth2 token requests and GraphQL calls can block indefinitely if the remote hangs, reintroducing the worker-wedge class of bug documented in MEMORY.md.
- **Kanıt:** `builder.Services.AddHttpClient<IkasTokenManager>(c => { c.Timeout = TimeSpan.FromSeconds(10); }); builder.Services.AddSingleton<IkasTokenManager>(); — last registration (Singleton) wins; typed-client registration is bypassed.`
- **Önerilen fix:** Remove the two AddSingleton<T>() lines (93 and 98). AddHttpClient<T> already registers T as transient with the correctly-configured HttpClient; IkasProvider (also singleton) will receive a new transient instance per its own constructor call, which is correct for named-client typed clients. If a true singleton is required, use the IHttpClientFactory pattern explicitly in the constructor and store it, rather than storing HttpClient directly.

#### Integrations-3 · [HIGH] review_alerts GRANT uses wrong production role (invekto_app instead of invekto)

- **Dosya:** `arch/db/pkt6b1-niche-business.sql` (92-93)
- **Boyut:** correctness · **Fix-risk:** low · behavior-preserving
- **Sorun:** The review_alerts table (and return_deflections, leads, lead_activities on lines 44-45, 193-194, 240-241) are granted to role 'invekto_app'. The production database role is 'invekto' — this is recorded as an explicit lesson in kanban-board.sql line 154, tenant-settings.sql line 88, and repeated in every migration since 2026-04-18. Using invekto_app means the application user has no permission to INSERT, UPDATE, or SELECT on review_alerts, causing every call to UpsertReviewAlertAsync, GetReviewAlertsAsync, and UpdateRecoveryStatusAsync to fail with PostgreSQL permission denied (42501).
- **Kanıt:** `arch/db/pkt6b1-niche-business.sql lines 92-93: 'GRANT ALL ON review_alerts TO invekto_app; GRANT USAGE, SELECT ON SEQUENCE review_alerts_id_seq TO invekto_app;'. arch/db/kanban-board.sql line 154: '-- Lesson 2026-04-18: invekto role (NOT invekto_app).'`
- **Önerilen fix:** In pkt6b1-niche-business.sql replace all four occurrences of 'invekto_app' with 'invekto'. A migration should also issue 'GRANT ALL ON review_alerts TO invekto; GRANT USAGE, SELECT ON SEQUENCE review_alerts_id_seq TO invekto;' (and equivalents for return_deflections, leads, lead_activities) against the live database.

#### Integrations-4 · [HIGH] Silent pagination loss: FetchOrdersAsync always returns at most 50 orders with no page-exhaustion loop

- **Dosya:** `src/Invekto.Integrations/Services/Ikas/IkasProvider.cs` (33-57)
- **Boyut:** correctness · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** FetchOrdersAsync sends a single GraphQL request with pagination.limit=50 and never inspects hasNextPage or hasNext in the response. ParseOrderList reads only the 'data' array and discards pagination metadata. For any tenant with more than 50 orders created or updated in the 7-day sync window, orders beyond position 50 are silently dropped from the cache. The IkasQueries.ListOrders query does request 'hasNext' (line 52) but the result is never consumed.
- **Kanıt:** `IkasProvider.cs line 50: 'variables["pagination"] = new { limit = 50 }'. ParseOrderList (lines 216-268) calls 'root.TryGetProperty("listOrder", out var listOrder)' and iterates only 'data' — no 'hasNext' or cursor check anywhere. IkasQueries.cs line 52: 'hasNext' is selected but never read.`
- **Önerilen fix:** Add a pagination loop in FetchOrdersAsync: after each page parse, check if hasNext==true (or hasNextPage in the parsed result) and issue a follow-up request with a cursor/offset parameter until exhausted or a page guard (e.g. max 10 pages) is hit. Alternatively, add a cursor field to EcommerceProductFilter and let the caller drive pagination. The loop approach is behavior-preserving for OrderSyncJob which expects a complete list.

#### Integrations-5 · [MEDIUM] ReadBoundedResponseAsync silently truncates responses without caller notification

- **Dosya:** `src/Invekto.Integrations/Services/Ikas/IkasGraphQlClient.cs` (137-151)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** ReadBoundedResponseAsync reads at most 256KB of the response stream and returns whatever bytes were read, with no indication that truncation occurred (totalRead == MaxResponseBytes does not set any flag). A truncated JSON body is then passed to JsonDocument.Parse which throws JsonException, caught at lines 131-134 as a generic 'Response parse error'. The actual GraphQL error or partial data is discarded. An operator seeing 'Response parse error' has no way to distinguish a truncated response from malformed JSON. Additionally, if a large product catalog or order list is legitimately returned (possible for merchants with many items), valid data is silently dropped.
- **Kanıt:** `Lines 143-148: loop reads up to MaxResponseBytes (256KB) and returns immediately after. No check for 'totalRead == MaxResponseBytes'. Caller at line 88 passes the possibly-truncated string to JsonDocument.Parse.`
- **Önerilen fix:** After the read loop, add: 'if (totalRead >= MaxResponseBytes) { _logger.SystemWarn($"[{ErrorCodes.IntegrationsEcomGraphQlFailed}] ikas response truncated at {MaxResponseBytes} bytes"); return IkasGraphQlResponse.Fail(ErrorCodes.IntegrationsEcomGraphQlFailed, "Response too large (truncated at 256KB)"); }' before returning the string to the caller. This surfaces the operational issue explicitly rather than letting it masquerade as a JSON parse error.

#### Integrations-6 · [LOW] SlowEquals early-exits on length mismatch, leaking shared-secret length via timing side-channel

- **Dosya:** `src/Invekto.Integrations/Endpoints/VideoMeetingEndpoints.cs` (143-150)
- **Boyut:** security · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The custom SlowEquals method returns false early when a.Length != b.Length (line 146). This leaks the exact byte-length of InternalServices:SharedSecret to an attacker who can make repeated requests with tokens of varying lengths and measure the response time differential. The .NET 8 BCL provides CryptographicOperations.FixedTimeEquals(ReadOnlySpan<byte>, ReadOnlySpan<byte>) which handles differing lengths in constant time. The current implementation's constant-time loop (lines 148-149) is correct for equal-length inputs but the length guard at line 146 negates the protection for length-probing attacks. This is an internal endpoint, so real exploitability is low, but the pattern is incorrect.
- **Kanıt:** `if (a.Length != b.Length) return false; — early return before entering the XOR loop.`
- **Önerilen fix:** Replace SlowEquals with: return CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(a ?? ""), System.Text.Encoding.UTF8.GetBytes(b)); — this handles unequal lengths in constant time. Add using System.Security.Cryptography;

### ChatAnalysis (6)

#### ChatAnalysis-1 · [HIGH] Broad catch(Exception) in ProcessAnalysisAsync swallows all background errors without INV-XXX code

- **Dosya:** `src/Invekto.ChatAnalysis/Program.cs` (318-322)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** The top-level background task in `ProcessAnalysisAsync` catches `Exception ex` broadly, logging only `ex.Message` with no error code. This is the repo hard-fail pattern. It absorbs `UriFormatException` from `new Uri(request.ChatServerURL)` on line 298, `NullReferenceException`, `OperationCanceledException` (which is a subclass of `Exception`), and any future regression. No INV-XXX code is emitted.
- **Kanıt:** `catch (Exception ex) { sw.Stop(); logger.StepError($"İşlem hatası: {ex.Message}", rid, sw.ElapsedMilliseconds); }`
- **Önerilen fix:** Replace with typed catches: `catch (UriFormatException ex)` for the `new Uri(request.ChatServerURL)` call on line 298 (this is the only realistic throw in the non-Claude path); `catch (OperationCanceledException)` for cancellation; log each with the nearest applicable INV-XXX error code. The SSRF check on entry ensures scheme is http/https and URI parses, so `UriFormatException` at line 298 is redundant but should still be typed.

#### ChatAnalysis-2 · [HIGH] Auth silently disabled when InternalApiKey config is empty

- **Dosya:** `src/Invekto.ChatAnalysis/Program.cs` (92-101)
- **Boyut:** security · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** The `/api/v1/analyze` endpoint only enforces the `X-Internal-Api-Key` check when `internalApiKey` is non-empty: `if (!string.IsNullOrEmpty(internalApiKey))`. If `Microservice:InternalApiKey` is missing or blank in production config, the entire auth gate is skipped and the endpoint is unauthenticated. The service does not validate this at startup (unlike `Claude:ApiKey` which throws on line 21-24). This is a silent open-door failure mode.
- **Kanıt:** `var internalApiKey = builder.Configuration["Microservice:InternalApiKey"] ?? ""; ... if (!string.IsNullOrEmpty(internalApiKey)) { ... }`
- **Önerilen fix:** Add a startup validation block alongside the existing Claude key check: `if (string.IsNullOrEmpty(internalApiKey)) throw new InvalidOperationException("FATAL: Microservice:InternalApiKey is not configured");`. Then the endpoint-level guard can be simplified to an unconditional check without the outer null guard.

#### ChatAnalysis-3 · [MEDIUM] WapCrmClient is dead code — defined but never registered or used

- **Dosya:** `src/Invekto.ChatAnalysis/Services/WapCrmClient.cs` (1-145)
- **Boyut:** duplication · **Fix-risk:** low · behavior-preserving
- **Sorun:** `WapCrmClient` is defined in full (104 lines of logic, a `WapCrm:SecretKey` config key, and a `WapCrmResult<T>` wrapper type) but is never registered in `Program.cs` and never referenced anywhere in the solution. The current flow receives messages via the inbound `ChatAnalysisRequest.MessageListObject` payload rather than fetching them through WapCRM. The `WapCrm:SecretKey` config entry in `appsettings.json` is also dead. This is a maintenance hazard: the `DefaultRequestHeaders` secret bug above will go unnoticed because this code never runs.
- **Kanıt:** `Grep across src\: only two hits — the class definition itself and a historical reference in Shared/Contracts/Inma/README.md. Program.cs has zero mentions of WapCrmClient.`
- **Önerilen fix:** If the fetch-from-WapCRM flow is not planned for the near term, delete `WapCrmClient.cs` and remove the `WapCrm:SecretKey` entry from `appsettings.json`. If it is planned, file it as a known stub and fix the `DefaultRequestHeaders` bug before enabling it.

#### ChatAnalysis-4 · [LOW] Null-forgiving operator (!) on MessageListObject after guard already proves non-null

- **Dosya:** `src/Invekto.ChatAnalysis/Program.cs` (182)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** Line 182 uses `request.MessageListObject!.Count` with the null-forgiving `!` operator. The repo hard-fail rule prohibits `!` — it must be replaced with a pattern-bind or explicit null guard. In this case the guard on line 154 (`request.MessageListObject != null && request.MessageListObject.Count > 0`) already guarantees non-null, so the `!` is both unsafe style and unnecessary.
- **Kanıt:** `jsonLogger.StepInfo($"Analiz isteği alındı ({request.MessageListObject!.Count} mesaj)", request.RequestID);`
- **Önerilen fix:** Remove `!`: `request.MessageListObject.Count` is safe here because the `hasMessages` guard on lines 154-180 already returned early if null. Simply write `request.MessageListObject.Count`.

#### ChatAnalysis-5 · [LOW] WapCrmClient uses DefaultRequestHeaders for X-CIB-SecretKey — forbidden per-request secret pattern

- **Dosya:** `src/Invekto.ChatAnalysis/Services/WapCrmClient.cs` (32)
- **Boyut:** security · **Fix-risk:** low · behavior-preserving · _(orijinal sev: high → düzeltildi)_
- **Sorun:** The repo rule requires per-request secrets to be set via `HttpRequestMessage` + `TryAddWithoutValidation`, never via `DefaultRequestHeaders`. `DefaultRequestHeaders.Add` can throw `FormatException` on certain secret-key characters and, more importantly, exposes the secret on every request sharing the HttpClient regardless of caller intent. The rule is unconditional for secret headers.
- **Kanıt:** `_httpClient.DefaultRequestHeaders.Add(SecretKeyHeader, _secretKey);`
- **Önerilen fix:** Remove the `DefaultRequestHeaders.Add` from the constructor. In `GetMessagesForPhoneAsync`, build an `HttpRequestMessage` and call `request.Headers.TryAddWithoutValidation(SecretKeyHeader, _secretKey)` before sending, mirroring the pattern used in other services (e.g., `MessageSenderService`). Also note: `WapCrmClient` is currently dead code (never registered in Program.cs) — this fix applies when it is brought live.

#### ChatAnalysis-6 · [LOW] SSRF check does not resolve hostnames — DNS-rebind and internal hostnames bypass the IP-range filter

- **Dosya:** `src/Invekto.ChatAnalysis/Program.cs` (326-362)
- **Boyut:** security · **Fix-risk:** medium · ⚠️ DAVRANIŞ DEĞİŞTİRİR · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** `IsAllowedCallbackUrl` blocks literal private IPs (10.x, 172.16-31.x, 192.168.x, 169.254.x) and well-known loopback hostnames. However, `IPAddress.TryParse` is only attempted when the host portion is a valid IP string. A hostname like `internal.corp` or `metadata.google.internal` (resolving to 169.254.169.254) passes straight through `return true` because `TryParse` returns false for hostnames. An attacker who controls the DNS record of a supplied URL can point it at a private IP, bypassing all checks.
- **Kanıt:** `if (System.Net.IPAddress.TryParse(host, out var ip)) { ... check IP ranges ... } return true; // hostname falls through here`
- **Önerilen fix:** Call `Dns.GetHostAddressesAsync(host)` before passing to the IP-range check, and apply the same private-range filter to every resolved address. If DNS resolution fails or any resolved address is private, return false. Alternatively, maintain an explicit allowlist of permitted callback hostnames/domains and deny all others — simpler and more secure given the controlled deployment context.

### WebChat (6)

#### WebChat-1 · [HIGH] catch(Exception) in FireWebhookForWidgetAsync and TriggerAIReplyAsync — forbidden broad catch

- **Dosya:** `src/Invekto.WebChat/Services/ConversationService.cs` (194-197, 291-293)
- **Boyut:** error-handling · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** Both fire-and-forget paths use `catch (Exception ex)` which is the exact hard-fail pattern. In TriggerAIReplyAsync (line 291) it catches ALL exceptions from DB calls, SignalR broadcast and AI HTTP — NpgsqlException, SocketException, OutOfMemoryException all get the same log line with no INV-XXX error code, making incident triage impossible. The `finally` block on line 295 removes the timer entry, but any OperationCanceledException (e.g. app shutdown) is also silently absorbed.
- **Kanıt:** `catch (Exception ex) { _logger.SystemError($"AI reply failed for conv {conversationId}: {ex.Message}"); } and catch (Exception ex) { _logger.SystemError($"Webhook fire failed for widget {widgetId}: {ex.Message}"); }`
- **Önerilen fix:** Split into typed catches: catch (NpgsqlException ex) { _logger.SystemError($"[INV-WC-DB] ...") } catch (OperationCanceledException) { /* re-throw or return */ } catch (Exception ex) { _logger.SystemError($"[INV-WC-023] Unexpected: {ex}") } — each branch uses the appropriate error code from arch/errors.md.

#### WebChat-2 · [HIGH] Request CancellationToken passed into fire-and-forget webhook lambdas — token will be cancelled before webhook fires

- **Dosya:** `src/Invekto.WebChat/Services/ConversationService.cs` (66-70, 108-112, 175-179)
- **Boyut:** correctness · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** All three webhook fire-and-forget calls (`_ = FireWebhookForWidgetAsync(...)`) capture the request-scoped `ct` in the lambda and pass it down to `_webhookClient.NotifyConversationCreatedAsync(..., ct)` (and similarly for visitor message and close). The request CT is cancelled as soon as the HTTP response is sent — which happens before the 5-second webhook timeout elapses. This means every webhook call will be reliably cancelled by the client disconnect, and the automation flows will silently never execute.
- **Kanıt:** `_ = FireWebhookForWidgetAsync(widgetId, wc => _webhookClient.NotifyConversationCreatedAsync(wc.TenantId, wc.FlowConversationCreated, conversationId, visitorId, name, email, pageUrl, ct));  // ct = ctx.RequestAborted`
- **Önerilen fix:** Pass CancellationToken.None (or a separate long-lived CancellationTokenSource linked to application lifetime via IHostApplicationLifetime) to the webhook notify methods inside the fire-and-forget lambda. The request CT must not cross the response boundary.

#### WebChat-3 · [MEDIUM] catch(Exception) in PushNotificationService.NotifyNewMessageAsync — forbidden broad catch

- **Dosya:** `src/Invekto.WebChat/Services/PushNotificationService.cs` (79-82)
- **Boyut:** error-handling · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR · _(orijinal sev: high → düzeltildi)_
- **Sorun:** The outer push-send try/catch uses bare `catch (Exception ex)` absorbing all failures — HttpRequestException, TaskCanceledException, JsonException — with only a log line. Called via `Task.Run()` from ConversationService (fire-and-forget) so unhandled exceptions would crash the ThreadPool task silently if the catch were removed, but the current form uses no INV-XXX error code and doesn't distinguish transient HTTP failures from token-parse errors.
- **Kanıt:** `catch (Exception ex) { _logger.SystemError($"Push notification failed: {ex.Message}"); }`
- **Önerilen fix:** Split: catch (HttpRequestException ex) { _logger.SystemError($"[INV-WC-021] Expo push HTTP fail: {ex.Message}") } catch (TaskCanceledException) { _logger.SystemError("[INV-WC-021] Expo push timed out") } catch (Exception ex) { _logger.SystemError($"[INV-WC-021] Push unexpected: {ex}") }

#### WebChat-4 · [MEDIUM] catch(Exception) in AIReplyService.GenerateReplyAsync — forbidden broad catch

- **Dosya:** `src/Invekto.WebChat/Services/AIReplyService.cs` (115-119)
- **Boyut:** error-handling · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR · _(orijinal sev: high → düzeltildi)_
- **Sorun:** After a correctly typed `catch (OperationCanceledException)` on line 110, a bare `catch (Exception ex)` swallows everything else — HttpRequestException (network), JsonException (malformed Claude response), OutOfMemoryException — all mapped to the same null return with no INV-XXX code. This follows the repo hard-fail pattern.
- **Kanıt:** `catch (Exception ex) { _logger.SystemError($"AI reply failed: {ex.Message}"); return null; }`
- **Önerilen fix:** Add catch (HttpRequestException ex) with [INV-WC-022] code, catch (JsonException ex) to distinguish malformed API response from network errors, keep catch (Exception ex) as last resort with full ex.ToString() so stack trace is preserved in logs.

#### WebChat-5 · [MEDIUM] catch(Exception) final fallthrough in AutomationWebhookClient.FireWebhookAsync

- **Dosya:** `src/Invekto.WebChat/Services/AutomationWebhookClient.cs` (112-116)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** FireWebhookAsync already correctly catches TaskCanceledException (line 102) and HttpRequestException (line 107) with INV-WC error codes. The final `catch (Exception ex)` on line 112 is a residual broad catch that would capture serialization errors, ObjectDisposedException on HttpClient, etc. — these would have the same error code as HTTP failures (WebChatWebhookFailed) which muddies the signal. Per repo rules even this final fallthrough must not be catch(Exception).
- **Kanıt:** `catch (Exception ex) { _logger.SystemError($"[{ErrorCodes.WebChatWebhookFailed}] Webhook unexpected error for flow {flowId}: {ex.Message}"); }`
- **Önerilen fix:** Replace with catch (Exception ex) { _logger.SystemError($"[{ErrorCodes.WebChatWebhookFailed}] Webhook unexpected ({ex.GetType().Name}) for flow {flowId}: {ex}"); } — at minimum log ex.GetType() + stack so ops can distinguish it from the HTTP-level WebhookFailed. If a distinct INV-XXX code exists for unexpected errors, use it.

#### WebChat-6 · [MEDIUM] Sync body read in /api/v1/auth/login can silently return empty content on chunked HTTP bodies

- **Dosya:** `src/Invekto.WebChat/Program.cs` (185-238)
- **Boyut:** correctness · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR
- **Sorun:** The login endpoint delegate is non-async (`(HttpContext ctx) =>` — not `async (HttpContext ctx) =>`). As a result it uses `JsonDocument.Parse(ctx.Request.Body)` (synchronous stream read) at line 190. ASP.NET Core request bodies are not synchronous-read-safe: on a chunked or large body this will either block the thread pool or read 0 bytes silently, causing JsonException or a payload of `{}`. All other endpoints in this file correctly use `await JsonDocument.ParseAsync(...)`. The login endpoint is also missing the `allowSynchronousIO` opt-in.
- **Kanıt:** `app.MapPost("/api/v1/auth/login", (HttpContext ctx) => { ... using var bodyDoc = JsonDocument.Parse(ctx.Request.Body);`
- **Önerilen fix:** Change the delegate to async: `async (HttpContext ctx) =>` and change line 190 to `using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);` consistent with all other endpoints.

### VoiceRuntime (4)

#### VoiceRuntime-1 · [LOW] Wrong error code reused on HTTP metrics auth rejections (INV-VR-004 is WS-specific)

- **Dosya:** `src/Invekto.VoiceRuntime/Endpoints/MetricsEndpoints.cs` (36-50)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** All three auth-rejection paths in /metrics/latency (missing token, invalid token, non-superadmin 403) emit ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed (INV-VR-004). Per ErrorCodes.cs line 732, INV-VR-004 is defined as 'Browser WS /ws/voice/microphone JWT/Origin/subprotocol mismatch'. The metrics endpoint is a plain HTTP REST endpoint — it has nothing to do with WS handshakes. An operator alerting on INV-VR-004 to diagnose WebSocket connection failures will receive false positives from HTTP 401/403 hits on this endpoint, making it impossible to distinguish WS-specific handshake failures from HTTP auth failures on the metrics route.
- **Kanıt:** `logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed}] [/metrics/latency] rejected: non-superadmin tenant={tenantContext.TenantId}"); return Results.Json(new { code = ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed, ... }, statusCode: StatusCodes.Status403Forbidden);`
- **Önerilen fix:** Define a dedicated INV-VR-025 (e.g. VoiceRuntimeMetricsAuthFailed) in ErrorCodes.cs for HTTP auth rejections on the metrics endpoint, and replace all three ErrorCodes.VoiceRuntimeWebSocketHandshakeFailed references in MetricsEndpoints.cs with it. Alternatively re-use the existing VoiceRuntimeImpersonationGateFailed (INV-VR-020) for the 403 non-superadmin case, and a new INV-VR-025 for the 401 missing/invalid token cases.

#### VoiceRuntime-2 · [LOW] /ready endpoint returns HTTP 200 for degraded state, defeating infrastructure health probes

- **Dosya:** `src/Invekto.VoiceRuntime/Endpoints/HealthEndpoints.cs` (27-29)
- **Boyut:** correctness · **Fix-risk:** low · ⚠️ DAVRANIŞ DEĞİŞTİRİR · _(orijinal sev: medium → düzeltildi)_
- **Sorun:** When the Silero VAD model file is missing or OPENAI_API_KEY is not set, /ready returns HTTP 200 with body {status:'degraded'}. Any health probe that checks the HTTP status code (NSSM service monitoring, load balancer, k8s readinessProbe) will consider the service ready. The comment explicitly notes this is intentional for dev convenience, but it means production deployments with a missing VAD model or absent API key will silently serve sessions that immediately fail (session fails at realtime.ConnectAsync with INV-VR-002) without the infrastructure knowing to hold traffic.
- **Kanıt:** `return missing.Count == 0 ? Results.Ok(...) : Results.Json(new { status = "degraded", ... }, statusCode: 200);`
- **Önerilen fix:** Return HTTP 503 (Service Unavailable) when missing items exist: replace `statusCode: 200` with `statusCode: StatusCodes.Status503ServiceUnavailable`. To preserve dev convenience, add a config flag (e.g. Ready:ReturnOkOnDegraded=true in appsettings.Development.json) so the 503 only fires in Production. This keeps the dev iteration cycle intact while fixing the production blind spot.

#### VoiceRuntime-3 · [LOW] BrowserRxLoopAsync logs unbounded browser text frames directly into the log line

- **Dosya:** `src/Invekto.VoiceRuntime/Endpoints/VoicePocEndpoints.cs` (793-797)
- **Boyut:** security · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** Text WebSocket messages from the browser are decoded and logged verbatim with no length cap: _logger.SystemInfo($"[VoicePoc/{sessionId}] browser control: {text}"). There is no size guard on text frames (binary frames correctly check pcmBytes.Length != Pcm48kFrameBytes and drop oversized frames). A connected client (JWT-gated, so requires a valid sysadmin token) could send a multi-megabyte text WS message that bloats the jsonl log file. Since this is sysadmin-only, this is low severity — but log-inflation attacks are cheap even for legitimate operators who accidentally send large payloads.
- **Kanıt:** `var text = Encoding.UTF8.GetString(accumulator.ToArray()); _logger.SystemInfo($"[VoicePoc/{_sessionId}] browser control: {text}");`
- **Önerilen fix:** Truncate the logged text: var logText = text.Length > 512 ? text[..512] + "...(truncated)" : text; and log logText. Optionally also add a max text frame size check (e.g. if (accumulator.Length > 4096) { _logger.SystemWarn(...); continue; }) before decoding.

#### VoiceRuntime-4 · [LOW] LatencyTracker.UpdateWindow enqueues outside the trim lock — window can transiently exceed RollingWindowSize by concurrent-caller count

- **Dosya:** `src/Invekto.VoiceRuntime/Metrics/LatencyTracker.cs` (56-63)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** UpdateWindow (lines 56-63) calls window.Enqueue(ms) at line 58 BEFORE entering the lock at line 59 that governs the trim loop. LatencyTracker is a singleton; RecordEvent can be called from multiple session threads concurrently. If N threads all call Enqueue before any of them acquires the lock, the window can transiently grow to RollingWindowSize + N. The trim loop inside the lock then drains back to RollingWindowSize, but in between, Snapshot() (called by the metrics endpoint) may see a slightly inflated window and slightly different p50/p95/p99 values. This is not a security or data-loss issue, but the rolling window size guarantee is not exact.
- **Kanıt:** `Lines 57-62: 'window.Enqueue(ms); lock (_windowLock) { while (window.Count > _rollingWindowSize && window.TryDequeue(out _)) { } }'`
- **Önerilen fix:** Move window.Enqueue inside the lock to make the enqueue + trim atomic:
lock (_windowLock) { window.Enqueue(ms); while (window.Count > _rollingWindowSize && window.TryDequeue(out _)) { } }
This ensures the window never exceeds RollingWindowSize at any observable point. Performance impact is negligible (lock is per-event, not per-frame).

### VoiceAI (4)

#### VoiceAI-1 · [CRITICAL] TraceIdentifier used raw in Windows file path — colon crashes every transcription call

- **Dosya:** `src/Invekto.VoiceAI/Services/VoiceTranscriptionService.cs` (42)
- **Boyut:** correctness · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The temp file path is built as `$"voiceai_{requestId}_{fileName}"` where `requestId` comes from `ctx.TraceIdentifier` (Program.cs:144). ASP.NET Core's TraceIdentifier format on Kestrel is `{connectionId}:{requestNumber}` (e.g. `0HMFK2N2GF62E:00000001`). The colon `:` is an illegal character in Windows file names (reserved for drive-letter syntax). `new FileStream(tempPath, FileMode.Create, ...)` on Windows throws `IOException: The filename, directory name, or volume label syntax is incorrect` on every single transcription request. The production server runs Windows, so this is a 100%-repro crash path.
- **Kanıt:** `Program.cs:144 `var requestId = ctx.TraceIdentifier;` — VoiceTranscriptionService.cs:42 `var tempPath = Path.Combine(Path.GetTempPath(), $"voiceai_{requestId}_{fileName}");``
- **Önerilen fix:** Sanitize the requestId before embedding it in a file name: `var safeId = requestId.Replace(':', '_').Replace('/', '_');` and use `$"voiceai_{safeId}_{safeFileName}"`. Alternatively use `Guid.NewGuid().ToString("N")` as the unique file discriminator, which is inherently path-safe and avoids leaking the connection ID into the file system.

#### VoiceAI-2 · [HIGH] User-controlled file.FileName used raw in Path.Combine — path traversal to arbitrary file write

- **Dosya:** `src/Invekto.VoiceAI/Services/VoiceTranscriptionService.cs` (42)
- **Boyut:** security · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** `fileName` originates from `file.FileName` (the multipart upload name, fully user-controlled) and is embedded directly in the temp path without sanitization. A file named `../../evil.mp3` passes the `IsSupportedFormat` check (extension `.mp3` is valid) and, when embedded in `Path.Combine(Path.GetTempPath(), $"voiceai_{requestId}_{fileName}")`, resolves via OS path normalization to a location outside the temp directory. On Windows: `C:\Users\...\Temp\voiceai_id_../../evil.mp3` normalizes to `C:\Users\...\Temp\voiceai_id_..\..\evil.mp3` → writes to `C:\Users\...\evil.mp3`. Combined with the colon bug above, the `FileStream` write would land at an attacker-chosen OS path (with arbitrary audio content). Note: only audio bytes are written, not executable code, but overwriting arbitrary files is a high-severity impact.
- **Kanıt:** `Program.cs:187 `svc.ProcessAsync(stream, file.FileName, ...)` — VoiceTranscriptionService.cs:42 `$"voiceai_{requestId}_{fileName}"` — IsSupportedFormat only validates extension, not path characters.`
- **Önerilen fix:** Strip the filename to a safe base name before use: `var safeFileName = Path.GetFileName(fileName);` (strips any directory components) and additionally replace any remaining path-special chars. The `Path.GetFileName` call alone removes directory traversal on both Windows and Linux. Then use only the safe extension for the temp file: `var ext = Path.GetExtension(safeFileName); var tempPath = Path.Combine(Path.GetTempPath(), $"voiceai_{safeId}{ext}");`

#### VoiceAI-3 · [LOW] Temp file cleanup failure logged with wrong error code INV-VA-003 (Whisper transcription failed)

- **Dosya:** `src/Invekto.VoiceAI/Services/VoiceTranscriptionService.cs` (122-124)
- **Boyut:** error-handling · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** The `finally` block catches `IOException` from temp file deletion and logs it with `ErrorCodes.VoiceAITranscriptionFailed` (INV-VA-003), which is defined as 'Whisper API transcription failed'. This code is semantically wrong — it is emitted for a file cleanup failure, not a Whisper API error. Monitoring/alerting systems that filter on INV-VA-003 to detect Whisper outages will see false positives from cleanup errors and vice-versa. There is no dedicated cleanup error code in the VA namespace; INV-VA-005 (TranscriptionLogFailed) is the closest but also wrong. The catch itself is correct (typed `IOException`, non-fatal, logs and continues).
- **Kanıt:** ``_logger.SystemWarn($"[{ErrorCodes.VoiceAITranscriptionFailed}] Temp file cleanup failed: {ex.Message}");` — ErrorCodes.cs:722 `VoiceAITranscriptionFailed = "INV-VA-003"` described as 'Whisper API transcription failed'.`
- **Önerilen fix:** Add a dedicated `VoiceAITempFileCleanupFailed = "INV-VA-007"` entry to both `ErrorCodes.cs` and `arch/errors.md`, and use it in the cleanup catch. Alternatively, log without an INV code (plain warning string) since temp cleanup is an infrastructure nuisance, not a business error requiring alerting.

#### VoiceAI-4 · [LOW] X-Internal-Api-Key set via Headers.Add instead of TryAddWithoutValidation on HttpRequestMessage

- **Dosya:** `src/Invekto.VoiceAI/Services/VoiceTranscriptionService.cs` (149)
- **Boyut:** security · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** `request.Headers.Add("X-Internal-Api-Key", _chatAnalysisInternalApiKey)` uses `HttpRequestHeaders.Add`, which validates the header value and throws `FormatException` if the value contains chars the validator rejects (e.g., embedded newline, CR). The value comes from appsettings config, so in practice it is a simple ASCII key and the risk is very low. However, the canonical repo pattern for attaching secrets on a per-request HttpRequestMessage is `TryAddWithoutValidation` (BackendIntakeClient.cs:107, AutomationClient.cs, etc.). This diverges from the established codebase pattern. This is NOT a cross-tenant secret leak (the HttpClient is single-purpose 'ChatAnalysis', the key is a platform-level config value, and the assignment is on `request.Headers` not `DefaultRequestHeaders`).
- **Kanıt:** `VoiceTranscriptionService.cs:149 `request.Headers.Add("X-Internal-Api-Key", _chatAnalysisInternalApiKey);` vs BackendIntakeClient.cs:107 `msg.Headers.TryAddWithoutValidation(TokenHeaderName, _sharedSecret);``
- **Önerilen fix:** Change to `request.Headers.TryAddWithoutValidation("X-Internal-Api-Key", _chatAnalysisInternalApiKey);` to match repo convention and eliminate the theoretical FormatException risk.

### Marketing (2)

#### Marketing-1 · [HIGH] FollowupStageJob: uncaught JsonException from corrupt stages JSONB leaves run row permanently in 'scheduled' state

- **Dosya:** `src/Invekto.Marketing/Services/Jobs/FollowupStageJob.cs` (104-124)
- **Boyut:** error-handling · **Fix-risk:** low · behavior-preserving
- **Sorun:** The call to `ResolveSequenceByIdAsync` at line 111 is only protected by `catch (NpgsqlException)`. Inside `ResolveSequenceByIdAsync` (lines 258-260), `JsonSerializer.Deserialize<List<FollowupStageConfig>>` on the raw stages JSONB column can throw `System.Text.Json.JsonException` if the stored JSONB is malformed. That exception is not caught at lines 113-124 and propagates uncaught through `ExecuteAsync`. Hangfire's `AutomaticRetry(Attempts=0)` marks the job failed in Hangfire's own tables, but `TerminalMarkAsync` is never called — the `event_followup_runs` row retains `status='scheduled'` indefinitely with no error_code recorded. Operators querying `event_followup_runs WHERE status='scheduled'` will see phantom scheduled rows that will never fire again, and the cause is invisible without grepping Hangfire's error log.
- **Kanıt:** `try { sequence = await ResolveSequenceByIdAsync(run.SequenceId, ct); } catch (NpgsqlException ex) { ... return; } — no catch(JsonException). ResolveSequenceByIdAsync line 258: Stages = System.Text.Json.JsonSerializer.Deserialize<List<FollowupStageConfig>>(reader.GetString(2), FollowupSequenceReposit…`
- **Önerilen fix:** Add `catch (System.Text.Json.JsonException ex)` alongside the existing `catch (NpgsqlException)` block at lines 113-124, calling `TerminalMarkAsync(runId, FollowupRunStatusValues.Failed, ErrorCodes.FollowupSequenceConfigInvalid, ...)` and returning. This mirrors the identical handling already present in `FollowupSequenceCache.RunFetchAsync` (lines 111-120 of FollowupSequenceCache.cs) where a `JsonException` is caught, logged with `ErrorCodes.FollowupSequenceConfigInvalid`, and rethrown to the caller.

#### Marketing-2 · [LOW] FollowupSequenceValidator: two identical compiled Regex instances allocated for the same pattern

- **Dosya:** `src/Invekto.Marketing/Services/FollowupSequenceValidator.cs` (23-29)
- **Boyut:** duplication · **Fix-risk:** trivial · behavior-preserving
- **Sorun:** `SlugPattern` (line 23) and `TemplateSlugPattern` (line 29) are both `new Regex("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.Compiled)` — identical pattern, identical options, two separate compiled Regex allocations. The XML doc for `TemplateSlugPattern` even states 'same charset/length as sequence slug'. `RegexOptions.Compiled` causes JIT compilation of the NFA at startup; having two identical instances doubles that cost. More importantly, if the pattern ever needs to change (e.g. to allow uppercase or extend length), the change must be made in two places — a maintenance hazard.
- **Kanıt:** `private static readonly Regex SlugPattern = new("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.Compiled); // line 23 ⏎ private static readonly Regex TemplateSlugPattern = new("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.Compiled); // line 29`
- **Önerilen fix:** Remove `TemplateSlugPattern` and replace its single use at line 85 (`!TemplateSlugPattern.IsMatch(stage.TemplateSlug)`) with `SlugPattern`. Rename `SlugPattern` to `SlugOrTemplateSlugPattern` if distinguishing the semantic roles in code is desired, but a single instance is sufficient.

## 8. Ek: Refute Edilen Bulgular (28) — şeffaflık

Review'da ileri sürülüp adversarial doğrulamada elendi (false-positive / sanctioned exception / yanlış severity).

- `src/Invekto.Integrations/Program.cs` — Review webhook: input validation runs before auth check, leaking business information to unauthenticated callers  
  _neden:_ The claim is a false positive. `JwtAuthMiddleware` is registered at Program.cs line 123 covering the `/api/v1/` prefix (which includes `/api/v1/reviews/webhook`). The middleware short-circuits at JwtAuthMiddleware.cs lin
- `src/Invekto.Integrations/Program.cs` — JsonException swallowed in fulfill and refund handlers — malformed body silently continues with empty request  
  _neden:_ The code at the cited lines is real: both fulfill (481-494) and refund (559-574) do catch JsonException, log a warning, and continue with default-constructed request objects. However the claimed harmful behavior does not
- `src/Invekto.Integrations/Data/IntegrationsRepository.cs` — GetTenantIdByClientIdAsync: 'result is int' pattern silently returns null if column type changes  
  _neden:_ The claim is speculative, not a current defect. The canonical schema at arch/db/integrations.sql line 9 defines `integration_accounts.tenant_id` as `INTEGER NOT NULL`. Npgsql boxes PostgreSQL integer as System.Int32, so 
- `src/Invekto.Integrations/Services/Ikas/IkasGraphQlClient.cs` — IkasGraphQlClient does not catch TaskCanceledException (timeout) in SendRequestAsync  
  _neden:_ The factual observation is real — SendRequestAsync (lines 125-134) has no TaskCanceledException catch — but the claim misrepresents this as a meaningful gap. OrderSyncJob.ExecuteSyncAsync has an explicit per-account catc
- `src/Invekto.Marketing/Data/MarketingRepository.cs` — GetActiveTreatmentsForResponseAsync has no LIMIT — unbounded result set  
  _neden:_ The code at lines 825-846 confirms no LIMIT clause — the factual observation is accurate. However, the severity rating and proposed fix are both wrong for this context, making this a reject.

First, the proposed fix (LIM
- `src/Invekto.Marketing/Data/FollowupSequenceRepository.cs` — OpenConnectionAsync publicly exposed — bypasses tenant-scoping contract  
  _neden:_ The claim is a design-smell opinion, not a real isolation bug. Reading the actual code at C:\CRMs\InvektoServices\src\Invekto.Marketing\Data\FollowupSequenceRepository.cs line 42 and the sole caller FollowupStageJob.cs l
- `src/Invekto.Marketing/Data/MarketingRepository.cs` — No NpgsqlException catch anywhere — DB errors carry no INV-XXX error code mapping  
  _neden:_ The claim incorrectly inspects only MarketingRepository.cs in isolation and concludes there is no NpgsqlException handling. The actual error-handling boundary in this codebase is the endpoint/handler layer, not the repos
- `src/Invekto.Knowledge/Services/ImportService.cs` — Broad catch(Exception) in ImportService without INV-XXX error code mapping  
  _neden:_ The broad `catch (Exception ex)` at ImportService.cs:91-96 unconditionally re-throws (line 95: `throw;`) after updating the document status to "error". It is a cleanup-and-rethrow guard, not a terminal handler. The claim
- `src/Invekto.Knowledge/Services/DocumentProcessingService.cs` — Broad catch(Exception) in ProcessPdfAsync — violates repo policy  
  _neden:_ The broad catch(Exception) at lines 148-153 is real, but the specific safety argument in the claim — that it silently absorbs OperationCanceledException from PdfPig, preventing graceful shutdown — is factually wrong. Pdf
- `src/Invekto.Knowledge/Services/TemplateExtractorService.cs` — TemplateExtractorService.ExtractAndCompareAsync does not catch JsonException — can bypass NpgsqlException handler and leave no error record  
  _neden:_ The core claim — that JsonException can bypass both typed handlers and leave no error record — is false. Every JSON operation reachable from within ExtractAndCompareAsync's try block is already internally guarded: ParseJ
- `src/Invekto.ChatAnalysis/Services/CallbackService.cs` — Broad catch(Exception) in CallbackService swallows unknown errors without INV-XXX code  
  _neden:_ The catch(Exception) at lines 84-87 is real, but the claim misapplies the repo's hard-fail rule to the wrong context. The rule targets request handlers that must return INV-XXX error codes to callers; CallbackService.Sen
- `src/Invekto.WebChat/Program.cs` — Bare catch in ValidateOperatorJwt swallows all exceptions without INV-XXX mapping  
  _neden:_ The bare `catch` at Program.cs:393-398 is real but the hard-fail rule as defined in this repo requires that a broad catch lack an INV-XXX error code mapping. Here the catch body explicitly returns `ErrorResponse.Create(E
- `arch/db/webchat.sql` — Missing GRANT on original webchat tables and sequences — runtime permission failure in production  
  _neden:_ The claim is rejected on two independent grounds.

First, the proposed fix uses the wrong role. The actual GRANT pattern throughout this file (webchat.sql lines 220-223) and in migration 047 (lines 153-156) is `GRANT ALL
- `src/Invekto.WebChat/Services/ConversationService.cs` — async void-equivalent Timer callback — exceptions escape silently and dispose race possible  
  _neden:_ The async-void delegate at line 224 is technically correct — `TimerCallback` is `delegate void` so `async _ => await ...` does create an async void path. However, the claimed crash path does not exist: `TriggerAIReplyAsy
- `src/Invekto.WebChat/Data/WebChatRepository.cs` — WidgetId not projected in GetActiveConversationsAsync / GetAllConversationsAsync — silent null in ConversationRow  
  _neden:_ The omission of `c.widget_id` from the two list queries is real and confirmed in the code (lines 138-139 and 171-172 in WebChatRepository.cs), but the claimed harm does not exist today and the "latent bug" framing is spe
- `src/Invekto.Appointments/Services/WaitlistService.cs` — WaitlistService: entry committed as 'notified' before notification is dispatched — silent divergence on notification failure  
  _neden:_ The ordering concern (DB commit before fire-and-forget dispatch) is real and confirmed at lines 67-68. However, three facts refute the claim's severity and several of its assertions:

1. The pattern is explicitly documen
- `src/Invekto.AgentAI/Services/EscalationNoteService.cs` — ConversationEntry DTO defined inside service project instead of Invekto.Shared — microservice isolation violation  
  _neden:_ ConversationEntry is used exclusively within Invekto.AgentAI — both references (EscalationNoteService.cs:28 and Program.cs:473-483) are in the same service project. No other service project references it. This is an inte
- `src/Invekto.WhatsAppAnalytics/Data/InsightRepository.cs` — GetQualityInsightAsync COUNT(*)==100 pagination guard uses wrong sentinel: misses exact-100 case and double-counts when == 100 boundary is hit  
  _neden:_ The claim's central failure scenario — "guard does NOT fire when tenant has 150 records" — is factually wrong. The first query uses LIMIT 100 (line 1351). When there are 150 records, the query returns exactly 100 rows, s
- `src/Invekto.WhatsAppAnalytics/Data/InsightRepository.cs` — No NpgsqlException handling anywhere in InsightRepository — DB errors propagate untyped  
  _neden:_ The claim overstates the severity and misidentifies the repo-level rule. Confirmed facts from reading the actual code:

1. InsightRepository has no NpgsqlException catch — factually true. The only catch in the file is a 
- `src/Invekto.WhatsAppAnalytics/Data/InsightRepository.cs` — GetQualityInsightAsync second query parameters added without clearing — potential duplicate parameter name if NpgsqlCommand reuses internal state  
  _neden:_ cmd2 is created fresh via conn2.CreateCommand() (line 1380) on a separate connection — it starts with zero parameters. Lines 1382-1384 then add "tid" and conditionally "iid" to cmd2, which is identical to the setup on th
- `src/Invekto.WhatsAppAnalytics/Data/AnalyticsRepository.cs` — Internal update methods lack tenant_id filter — cross-tenant write possible  
  _neden:_ The four update methods (UpdateAnalysisStatusAsync, UpdateAnalysisTotalsAsync, CompleteAnalysisAsync, FailAnalysisAsync) are called exclusively from internal background services (AnalysisProcessingService and PipelineOrc
- `src/Invekto.WhatsAppAnalytics/Services/RiDashboardService.cs` — SafeGet swallows OperationCanceledException — cancellation silently becomes null response  
  _neden:_ The bare `catch { return null; }` at RiDashboardService.cs:102-104 and OnboardingInsightService.cs:207-209 is real code, but the specific high-severity scenario described — request CancellationToken being silently swallo
- `src/Invekto.WhatsAppAnalytics/Services/ClaudeClient.cs` — ClaudeClient x-api-key sent via request.Headers.Add — FormatException risk on malformed config secret  
  _neden:_ Two material errors in the claim make it unreliable, though there is a kernel of a real pattern deviation.

1. **Wrong failure mode.** The reviewer claims a `FormatException` would be "caught by the outer HttpRequestExce
- `src/Invekto.VoiceRuntime/Endpoints/VoicePocEndpoints.cs` — Task.WhenAny on three pipeline loops does not await the remaining two tasks after one completes  
  _neden:_ All three loop methods (BrowserRxLoopAsync at line 764, VoiceToRealtimeForwardLoopAsync at line 815, BrowserTxLoopAsync at line 865) each contain a top-level try/catch that covers OperationCanceledException, WebSocketExc
- `src/Invekto.VoiceRuntime/Realtime/RealtimeApiClient.cs` — DispatchEvent catch scope too narrow — non-JsonException from synchronous handler invocations silently faults the receive loop  
  _neden:_ The claim's core consequence chain is factually wrong. The critical step is at line 348 in DisposeAsync: the code uses `await Task.WhenAny(Task.WhenAll(_sendLoopTask, _recvLoopTask), Task.Delay(2000))` — NOT a direct `aw
- `src/Invekto.VoiceRuntime/Tools/SearchKnowledgeBaseTool.cs` — SearchKnowledgeBaseTool registers Linq using directive but only .Max() and .Where() are used — Linq namespace brought in globally  
  _neden:_ The technical observation is accurate: System.Linq IS redundant at SearchKnowledgeBaseTool.cs line 1 because ImplicitUsings=enable in the csproj causes the SDK to emit it in the auto-generated GlobalUsings.g.cs (confirme
- `src/Invekto.VoiceRuntime/Tools/ToolExecutor.cs` — ToolExecutor.OnArgumentsDoneAsync outer catch list missing KnowledgeSearchException — if a future IVoiceTool violates its no-throw contract the exception becomes an unobserved task fault  
  _neden:_ The claim is a false positive. The code at SearchKnowledgeBaseTool.cs lines 209-215 confirms KnowledgeSearchException is caught and converted to a ToolExecutionResult error shape inside ExecuteAsync — it never escapes in
- `src/Invekto.VoiceRuntime/Endpoints/VoicePocEndpoints.cs` — VoicePocEndpoints impersonation gate: devBypass path sets callerTenantId=0 and skips the sysadmin check — a ?dev=1 caller with any tenantId in a dev JWT can bypass the opsOnly gate  
  _neden:_ The code at lines 97-149 confirms the mechanism is real — devBypass sets callerTenantId=0 and skips the sysadmin gate — but the claim overstates the risk and mislabels it as a medium-severity security bug rather than int

---
_Üretim: read-only multi-agent audit, 2026-06-14. Kod değişikliği yapılmadı._
