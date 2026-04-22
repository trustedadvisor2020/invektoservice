# P10 — FEAT-EFS Hangfire Marketing-Followup Queue Fix

> **Slug:** `20260423-feat-efs-hangfire-queue-fix`
> **Roadmap ref:** `tracking/pilot-launch-roadmap.md` §FAZ 5 (P9 escalation)
> **Plan JSON:** `arch/plans/20260423-feat-efs-hangfire-queue-fix.json`
> **Risk:** MEDIUM (code + production data cleanup, no schema migration)
> **Status:** PLANNED (2026-04-23 interview closed, ready for dev)
> **Predecessor:** P9 S7b CRITICAL FAIL (Hangfire Scheduled→Enqueued pickup gap)

---

## Scope

P9 Dent smoke S7b FAIL root cause investigation tespit etti ki **roadmap'teki 3 fix option hipotezi yanlıştı**. Gerçek root cause:

**Orphan `default` queue** — birkaç Hangfire job ([Queue] attribute'u OLMADAN) registered → `BackgroundJob.Schedule<T>` ve `RecurringJob.AddOrUpdate<T>` default=`"default"` queue kullanıyor → 5 server'ın hiçbiri `"default"` queue'yu dinlemiyor (hepsi dedicated named queue: backend/automation/appointments/integrations/marketing-followup) → jobs sonsuza kadar `hangfire.jobqueue.default` tablosunda sıkışıp kalıyor.

**Evidence (2026-04-22 11:38 UTC prod):**
- `hangfire.jobqueue` 1019 rows, tamamı `queue='default'`, tamamı ApiKeyRateLimiter.SweepNow, en eski 2026-04-18 22:35Z (4 gün latent)
- `hangfire.server` 5 active heartbeat: backend/automation/appointments/integrations/marketing-followup — hiçbiri default'u dinlemiyor
- `Hangfire.AspNetCore 1.8.14` + `Hangfire.PostgreSql 1.20.10`

**Affected components (2 latent bug):**
1. `ApiKeyRateLimiter.SweepNow` — `src/Invekto.Backend/Program.cs:701-704` RecurringJob registered without queue → `default`
2. `FollowupStageJob` — `src/Invekto.Marketing/Services/Jobs/FollowupStageJob.cs:36` class-level [Queue] attribute eksik → `BackgroundJob.Schedule<FollowupStageJob>` from `src/Invekto.Marketing/Services/FollowupOrchestrator.cs:191` → `default`

P9 EFS drip dead-on-arrival root cause AYNI pattern — FollowupStageJob'un default queue'ya gitmesi, Marketing worker'ın marketing-followup dinlemesi.

**Out of scope:**
- Roadmap'teki 3 option (a: Backend queues ext, b: Marketing advisory-lock scheduler, c: DelayedJobScheduler patch) — hepsi yanlış hipoteze dayalıydı
- Schema migration (sadece prod data cleanup 1019 orphan row)
- Kapsamlı BackgroundJob call-site audit (comprehensive audit ayrı paket, Q onayıyla)

---

## Interview Gates (kapali, 2026-04-22)

| # | Soru | Q Cevap |
|---|------|---------|
| G1 | Root cause verification önce mi direkt fix mi? | **Önce verify** — 3 option'dan birini uygulamadan prod state investigate. Sonuç: gerçek bug farklıydı, roadmap hipotezi yanlış. |
| G2 | P10 scope şekli | **Minimal fix + cleanup + guard** (Recommended). 2 line code + 1019 orphan DELETE + startup WARN guard. |
| G3 | Comprehensive audit zaman | Ayrı paket (scope creep kaçın). Future: ya Roslyn analyzer ya custom JobFilter ile auto-assignment. |
| G4 | Deploy scope | Backend + Marketing (Migration YOK). Re-smoke S7b+S8 post-deploy. |

---

## Changes (4 file)

### 1. `src/Invekto.Marketing/Services/Jobs/FollowupStageJob.cs`
- Class-level attribute ekle: `[Hangfire.Queue("marketing-followup")]`
- Mevcut `[Hangfire.AutomaticRetry(Attempts = 0, ...)]` attribute'una komşu yerleştir (aynı FQN pattern; using ekleme gereksiz)
- Tek invocable method (`ExecuteAsync`) mevcut olduğu için class-level canonical — diğer tüm job class'ları aynı pattern'i kullanıyor (VideoMeetingCreationJob, RescueFollowUpJob, FlowWaitResumerJob, ReminderJob, OrderSyncJob)

### 2. `src/Invekto.Backend/Services/ApiKeyRateLimiter.cs` (method `SweepNow`)
- Method-level attribute ekle: `[Hangfire.Queue("backend")]`
- Class-level DEĞİL — ApiKeyRateLimiter bir service class, non-Hangfire method'lar da içerir (`TryAcquire`, `Sweep(DateTime)`), class-level attribute bunları yanlışlıkla tag'lerdi
- `Hangfire.QueueAttribute` `AttributeTargets.Class | AttributeTargets.Method` destekler — method-level API-legal
- Doc comment P10 context + rationale ile güncellendi

### 3. `src/Invekto.Backend/Program.cs` (~line 701-704 + ~line 534+)
**(a) SweepNow recurring job registration** — 3-arg kalıyor, queue artık method attribute'undan türüyor:
```csharp
RecurringJob.AddOrUpdate<ApiKeyRateLimiter>(
    "lead-intake:rate-limiter-sweep",
    limiter => limiter.SweepNow(),
    "*/5 * * * *");
```
Comment güncellendi — attribute-based rationale belirtildi.

> **Discovery note:** Initial draft `new RecurringJobOptions { Queue = "backend" }` 4-arg overload'unu denedi ama Hangfire 1.8.14 `RecurringJobOptions` class'ında `Queue` property'si **yok** (build CS0117). Attribute-based canonical pattern'e geçildi (diğer servislerde de bu pattern kullanılıyor, `RecurringJobOptions.Queue` hiçbir caller'da yok).

**(b) Startup orphan default queue guard** — `if (hangfireEnabled)` block'unun sonunda, existing score=-1 nudge'dan hemen sonra:
```csharp
try
{
    using var orphanConn = new Npgsql.NpgsqlConnection(hangfireConnStr);
    orphanConn.Open();
    using var orphanCmd = orphanConn.CreateCommand();
    orphanCmd.CommandText = "SELECT COUNT(*) FROM hangfire.jobqueue WHERE queue = 'default';";
    var orphanCount = Convert.ToInt64(orphanCmd.ExecuteScalar() ?? 0L);
    if (orphanCount > 0)
    {
        app.Logger.LogWarning(
            "[INV-JOB-006] {Count} job(s) stuck on 'default' queue — no server listens to it. " +
            "Missing [Queue(...)] attribute on job class/method or RecurringJobOptions.Queue on registration. " +
            "Drain via: DELETE FROM hangfire.jobqueue WHERE queue='default';",
            orphanCount);
    }
}
catch (Npgsql.NpgsqlException ex)
{
    app.Logger.LogWarning(ex, "[INV-JOB-006] Guard probe failed (non-fatal).");
}
```
Non-blocking, typed Npgsql catch, structured log `[INV-JOB-006]` prefix (ops greppable).

### 4. `arch/errors.md` (new INV-JOB-006 canonical entry)
INV-JOB-005 ile INV-JOB-010 arası yerleştirildi:
```yaml
  - code: INV-JOB-006
    description: Hangfire orphan 'default' queue detected at startup. Jobs scheduled without explicit queue routing (missing [Queue(...)] attribute on the job class/method OR missing RecurringJobOptions.Queue on the registration) accumulate on the 'default' queue that no service worker listens to in this named-queue microservice topology. Surfaced as a structured WARN log at Backend startup (`[INV-JOB-006]` tag) with the stuck row count and drain SQL so ops can detect and remediate within the first deploy window rather than after a multi-day accumulation. Non-blocking probe; does not fail startup.
    user_message: Zamanlanmış görev altyapısında dinlenmeyen bir kuyrukta bekleyen iş tespit edildi (operasyonel uyarı).
```

---

## Acceptance Criteria

| # | Criterion | Verification Method |
|---|-----------|---------------------|
| AC1 | `FollowupStageJob` class'ı `[Hangfire.Queue("marketing-followup")]` attribute'una sahip | grep `FollowupStageJob.cs` için `[Hangfire.Queue(` |
| AC2 | `ApiKeyRateLimiter.SweepNow()` method'u `[Hangfire.Queue("backend")]` attribute'una sahip (method-level, class-level değil — diğer method'lar tag'lenmesin diye) | grep `ApiKeyRateLimiter.cs` için `[Hangfire.Queue("backend")]` |
| AC3 | Backend startup'ta `[INV-JOB-006]` WARN log emit ediliyor (orphan count > 0 iken). `arch/errors.md` INV-JOB-006 canonical entry mevcut | Prod log grep post-deploy; deploy öncesi 1019 > 0 beklenir; errors.md grep `INV-JOB-006` |
| AC4 | Prod DELETE cleanup: `hangfire.jobqueue WHERE queue='default'` count 0 (deploy sonrası, yeni SweepNow backend queue'ya gidiyor) | MCP SELECT COUNT post-deploy + post-5-dk-window |
| AC5 | Re-smoke P9 S7b+S8: yeni fake Dent lead + EFS trigger + test_mode=true + stage[0] scheduled_at +3 min; 3-5 min sonra `hangfire.job.statename='Succeeded'` + `event_followup_runs.status='sent'` + Marketing log `[FEAT-EFS][SEND-INTENT]` | MCP query + server-logs search + event_followup_runs DB |
| AC6 | Build PASS (dotnet 0 error); Codex review PASS (LOW/MEDIUM risk, iter target 0-1) | `dotnet build` exit 0 + `/rev` verdict=PASS |

---

## Rollback Plan

| Trigger | Action |
|---------|--------|
| Build fail | Revert changes (git diff'te 3 dosya) |
| Codex FAIL iter > 2 | Q onayli FORCE PASS veya rollback |
| Deploy HEALTHY fail | NSSM auto-restart; config preserve sandwich ile geri rollback (bak dosyaları) |
| Re-smoke S7b FAIL again | Daha derin investigate (Hangfire server worker registration, queue name casing); P10 re-scope |

---

## Deploy Plan

1. Pre-deploy: migrasyon YOK (schema değişikliği yok)
2. `dotnet publish -c Release` Backend + Marketing
3. MCP `invekto-ops server-deploy`:
   - Backend (tek node, queue=backend heartbeat koruyarak)
   - Marketing (tek node, queue=marketing-followup heartbeat koruyarak)
4. Health 10/10 HEALTHY verify (90 sec NSSM auto-restart window)
5. Post-deploy: MCP SQL DELETE 1019 orphan ApiKeyRateLimiter.SweepNow rows
6. Re-smoke S7b+S8:
   - Fake Dent lead INSERT (phone=+00000000001, name='SMOKE_TEST_P10_...')
   - `tenant_settings.efs_test_mode=true` flip
   - `event_followup_sequences id=1 enabled=true` flip
   - POST `/api/internal/followup/trigger` (Backend proxy)
   - 3-5 dk bekle
   - Verify: `hangfire.job statename='Succeeded'`, `event_followup_runs.status='sent'`, Marketing log `[FEAT-EFS][SEND-INTENT]`
7. Baseline restore (smoke cleanup): `SMOKE_TEST_P10_%` DELETE, `test_mode=false`, `enabled=false`

---

## References

- P9 blocker: `tracking/20260428-dent-pilot-smoke.md` §Discovered Blockers §2 Hangfire
- Root cause evidence: prod forensic 2026-04-22 11:38 UTC (5 server heartbeat, 1019 stuck queue='default')
- Hangfire 1.8.14 `BackgroundJobServerOptions.Queues` semantics: worker-pickup filter only; DelayedJobScheduler queue-agnostic (promotes jobs to their ORIGINAL queue, regardless of server's Queues list)
- Spec: `arch/features/event-followup-sequence.md`
- P5 plan: `arch/plans/20260425-feat-efs-drip-sequence.json` (FollowupStageJob architectural decisions §8 SEND-INTENT stub)
