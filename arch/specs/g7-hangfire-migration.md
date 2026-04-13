# SPEC: G7 Hangfire Migration

> **Spec ID:** SPEC-008 | **Paket:** G7 | **Risk:** HIGH (multi-service, scheduler substitution)
> **Yazar:** Q | **Son Guncelleme:** 2026-04-13 | **Durum:** DRAFT

## 1. Intent (Ne & Neden)

Mevcut 10 `IHostedService` tabanli in-process scheduler (Timer + Interlocked loop) uretim ortaminda su sinirliliklari yasatiyor:

- **Gozlemlenemez:** Son job calismasi, basari/hata gecmisi, kuyruktaki isler yok. Ops `/ops/flow-waits` gibi ad-hoc endpoint'lerle sorguluyor.
- **Restart-kayipli:** Servis restart'i acik timer'lari keser; bir sonraki dongu kacir (FlowWaitResumer 60s'lik boslugu sifirdan acar).
- **Tek-node:** Ayni servis iki instance'ta kalkarsa Interlocked guard sadece in-process calisir — cross-process overlap olur (deploy siralamasinda deja vu).
- **Retry primitive:** Try-catch + next-cycle retry. Exponential backoff, dead-letter, structured failure history yok.

Hangfire (PostgreSql storage) bu problemleri cozer: persistent recurring job state, dashboard, automatic retry (backoff), server-level leader election, structured monitoring.

## 2. Acceptance Criteria

| # | Kriter | Dogrulama Yontemi |
|---|--------|-------------------|
| AC-1 | `Hangfire.AspNetCore` + `Hangfire.PostgreSql` NuGet 5 hedef servise eklenmis (Faz 1: Appointments + Automation; Faz 2-5: kalan 3 servis) | `dotnet list package` |
| AC-2 | Hangfire schema ortak PG'de `hangfire` schema altinda, tum servisler ayni storage'i paylasir | DB query: `SELECT table_schema FROM information_schema.tables WHERE table_schema='hangfire'` |
| AC-3 | Faz 1 pilot: `ReminderSchedulerService` ve `FlowWaitResumerService` Hangfire recurring job'a cevrilmis, eski `IHostedService` silinmis | Codex diff + grep |
| AC-4 | `/hangfire` dashboard Backend'te mount, superadmin (tenant_id=0) JWT guard | Manual: login → /hangfire acilir; ordinary tenant 403 |
| AC-5 | Multi-tenant job'lar tenant_id'yi argumanda tasir, handler icinde scope kurar | Codex CQ + plan intentional_exclusion |
| AC-6 | Cross-tenant job'lar (FlowWaitResumer, NightlyBatch) `null` tenant ile global recurring — service-wide worker olarak calisir (by-design) | Spec decision + Codex codex_note |
| AC-7 | Build PASS; tum Faz 1 servis deploy-ready (NSSM etkilenmez) | `dotnet build` + smoke |
| AC-8 | Codex PASS Faz 1 plan JSON icin (iter<=2) | `/rev` verdict PASS |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| **Her servis kendi Hangfire server, ortak PG storage** | Mikroservis izolasyonu korunur; merkezi scheduler yeni bir servis + HTTP hop demek. Hangfire PG'nin leader election'i zaten multi-server-safe. | EXPECTED: Birden fazla `AddHangfireServer()` cagrisi yanlis degil — her servis kendi queue'sunu dinler. |
| **Queue-per-service** | Isolation: bir servis diger servisin job'ini enqueue etmez. Queue ismi = servis ismi (`appointments`, `automation`, `backend`, `integrations`, `whatsappanalytics`). | EXPECTED: `BackgroundJobServerOptions.Queues` her servis Program.cs'te farkli. |
| **Cross-tenant recurring'de tenant_id yok** | FlowWaitResumer, NightlyBatch zaten service-wide iterate eder (DB'den due listesi cekip her row'u tenant-scoped isler). Job argumanina tenant_id koymak yanilticidir. | EXPECTED: CQ7 tenant_id kontrolu bu job'larda N/A. |
| **Tenant-scoped tek-seferlik job (delay / flow wait) tenant_id + ilgili PK'yi argumanda tasir** | Hangfire JSON serializer, primitive args guvenli. Entity referansi yerine id. | EXPECTED: CQ7 tenant_id argumanda bekleniyor. |
| **Dashboard /hangfire path'inde, Backend host'unda** | Tek login surface. `/hangfire` reverse-proxy gerekmeden Backend uzerinden ACL'li. | EXPECTED: No new microservice added for UI. |
| **Strangler: Faz 1 pilot (2 servis) → Faz 2-5 kalan 8 servis** | 10 servisi tek PR'da cevirmek yuksek FAIL olasiligi. Hangfire ogrenme egrisi + prod davranis farklari faz-by-faz izlenir. | EXPECTED: Faz 1 plan allowed_files sadece 2 servis + Shared + Backend dashboard wire-up. |
| **`IHostedService` silinmez; Hangfire registration DI'da yapilir, eski class `[Obsolete]` degil direkt kaldirilir** | PP-006: half-done implementation yok. Eski timer kodu silinince Codex CQ yanilmaz. | EXPECTED: Delete, not deprecate. |
| **Recurring job registration Program.cs startup'ta idempotent** | `RecurringJob.AddOrUpdate` zaten idempotent; ikinci register override eder. Yeni servis register olunca eski job orphan kalmaz (cleanup: manuel ya da sonraki fazda). | EXPECTED: Startup double-register safe. |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| DB Schema (Hangfire-managed) | Migration: `arch/db/migrations/011-hangfire-schema.sql` (sadece `CREATE SCHEMA hangfire; GRANT USAGE...`) — Hangfire auto-creates tables on first run. |
| Error Codes | `arch/errors.md` INV-JOB-001..005 (yeni namespace) |
| Dashboard Auth | `arch/auth-architecture.md` — superadmin-only endpoint pattern reuse |

## 5. Scope Boundaries

### In Scope (Spec overall)
- 10 scheduled/recurring `IHostedService` → Hangfire recurring job
- Queue-per-service topology
- `/hangfire` dashboard, superadmin guard
- Multi-tenant job argumani convention
- Migration 011 (schema + grant)
- Error codes INV-JOB-001..005

### In Scope (Faz 1 — bu paket)
- Hangfire NuGet + DI altyapisi (`Invekto.Shared.Hosting.HangfireSetup` extension)
- `ReminderSchedulerService` → `ReminderJob` (Appointments, queue=`appointments`)
- `FlowWaitResumerService` → `FlowWaitResumerJob` (Automation, queue=`automation`)
- Backend `/hangfire` dashboard mount + superadmin guard
- Migration 011
- Error codes INV-JOB-001..005

### Out of Scope (Explicit)
- Queue consumer worker'lar (MessageSender, AnalysisProcessing, DocumentProcessing, BenchmarkProcessing, BatchClassification) — Hangfire'in use-case'i degil, DB queue pull loop'u kalir.
- SimulationEngine (in-memory TTL cache cleanup) — Hangfire gereksiz overhead.
- Kalan 8 scheduler (CronScheduler, RescueFollowUp, TreatmentLifecycle, Waitlist, TranslationCleanup, MetricsAggregation, OrderSync, NightlyBatch) — **Faz 2-5**.
- Hangfire Pro (Redis storage, batches) — not licensed, not needed.
- Dashboard per-tenant filtering — superadmin gorur, tek surface.

### Degismeyen Alanlar (Pre-existing)
- `flow_execution_state` tablosu (G6'da eklendi) — Hangfire job sadece okur, `ResumeWaitAsync` delegasyonu dokunulmaz.
- Appointments reminder DB schema + 48h/2h logic — handler icindeki is mantigi byte-identical tasinir.
- NSSM service names, deploy process — degisiklik yok.

## 6. Service Boundaries

| Servis | Rol | Faz | Degisiklik Tipi |
|--------|-----|-----|-----------------|
| Invekto.Shared | HangfireSetup extension, JobArgs DTO'lar, ErrorCodes | Faz 1 | Yeni dosyalar |
| Invekto.Backend | `/hangfire` dashboard mount + superadmin guard, global Hangfire server (queue=`backend`, Faz 3+ icin hazir) | Faz 1 | Program.cs + auth filter |
| Invekto.Appointments | ReminderScheduler → ReminderJob | Faz 1 | Replace |
| Invekto.Automation | FlowWaitResumer → FlowWaitResumerJob | Faz 1 | Replace |
| Invekto.Appointments | Waitlist, TreatmentLifecycle | Faz 2 | Replace |
| Invekto.Automation | CronScheduler, RescueFollowUp | Faz 3 | Replace |
| Invekto.Backend | TranslationCleanup, MetricsAggregation | Faz 4 | Replace |
| Invekto.Integrations + WhatsAppAnalytics | OrderSync, NightlyBatch | Faz 5 | Replace |

## 7. Risk & Mitigation

| Risk | Olasilik | Mitigation |
|------|----------|------------|
| Hangfire PG storage baslangic migration ortak DB'ye yazarken race | LOW | Migration 011 sadece `CREATE SCHEMA + GRANT`. Hangfire tablolarini kendi `PrepareSchemaIfNecessary=true` ile olusturur, concurrent-safe (advisory lock). |
| Birden fazla servis ayni recurring job id'yi register ederse cakisma | MEDIUM | Job id namespace: `<service>:<job-name>` (orn. `appointments:reminder`). Queue-per-service zaten izolasyon saglar. |
| Dashboard'da sensitive data leak (job argumanlari tenant_id icerir) | MEDIUM | Superadmin-only guard. `DashboardAuthorizationFilter` JWT tenant_id=0 kontrol. |
| Graceful shutdown: mevcut timer `StopAsync` ile 30s bekliyor; Hangfire `BackgroundJobServer.Dispose()` ayni davraniyor mu? | LOW | Hangfire `ShutdownTimeout=30s` config ile ayarlanir. |
| Cross-tenant FlowWaitResumerJob 60s cron — Hangfire min recurring interval 60s mi? | LOW | Hangfire tam destekler; `Cron.MinuteInterval(1)` veya `Cron.Minutely()`. |
| Eski IHostedService siliniminden sonra deploy edilmemis servis hala eski kodla calisir → double-process | HIGH | Deploy sirasi: migration → tum Faz 1 servisleri ayni anda deploy. NSSM stop-all → upload-all → start-all. `/deploy` skill bu sirayi destekler. |
| Reminder T-48h/T-2h logic job parametresiz cron'da iki kere cagrilir mi? | LOW | Tek recurring job icinde iki batch sirayli islenir (mevcut davranis korunur). |

## 8. Implementation Notes (Faz 1)

- `Invekto.Shared/Hosting/HangfireSetup.cs`: extension `AddInvektoHangfire(string queueName, string connectionString)` — storage options, server options, JSON serializer, retry policy (ExponentialBackoff attribute default).
- `Invekto.Shared/Jobs/IJob.cs`: marker interface; her job handler class'i bunu implement eder (DI resolve icin).
- Backend `Program.cs`:
  ```
  app.UseHangfireDashboard("/hangfire", new DashboardOptions {
      Authorization = new[] { new SuperAdminDashboardFilter() }
  });
  ```
- `SuperAdminDashboardFilter : IDashboardAuthorizationFilter` — HttpContext.User'dan tenant_id claim oku, == 0 degilse false.
- Appointments `Program.cs`: `services.AddInvektoHangfire("appointments", connStr); services.AddScoped<ReminderJob>();` + startup'ta `RecurringJob.AddOrUpdate<ReminderJob>("appointments:reminder", j => j.RunAsync(), "*/5 * * * *");`
- Automation similar: `RecurringJob.AddOrUpdate<FlowWaitResumerJob>("automation:flow-wait-resumer", j => j.RunAsync(CancellationToken.None), Cron.Minutely());`
- Job handlers repository + HTTP client'leri DI'dan alir; mevcut is mantigi ` ProcessReminders` ve `ResumeFlowWaitsAsync` metotlarindan byte-identical kopyalanir.

## 9. Faz Tablosu

| Faz | Scope | Tahmini Sure | Risk |
|-----|-------|-------------|------|
| Faz 1 | Altyapi + Reminder + FlowWaitResumer + Dashboard | 1-2 gun | HIGH (ilk kurulum) |
| Faz 2 | Waitlist + TreatmentLifecycle | 0.5 gun | MEDIUM |
| Faz 3 | CronScheduler + RescueFollowUp | 1 gun | MEDIUM |
| Faz 4 | TranslationCleanup + MetricsAggregation | 0.5 gun | LOW |
| Faz 5 | OrderSync + NightlyBatch | 0.5-1 gun | MEDIUM |

Her faz ayri plan JSON + Codex review.

## 12. Architectural Exception: Scheduler Host (2026-04-13)

**Problem:** Faz 5 tamamlandiktan sonra production'da 9 recurring job'dan 7'si `Hangfire.Common.JobLoadException: Could not load the job` ile fail etti. Neden: Her servis kendi `IHostedService` startup'inda `RecurringJob.AddOrUpdate<T>(...)` cagiriyordu; Hangfire job definition'a job type'in assembly-qualified name'ini persist ediyor. Leader scheduler (Backend) her dakika due job'lari kuyruga yazmak icin `Type.GetType(assemblyQualifiedName)` yapiyor — Backend'in `bin/` klasorunde diger servislerin DLL'leri olmadigi icin resolve edemiyor ve job `throw` ediyor.

**Karar:** Backend `.csproj` dosyasina `Appointments`, `Automation`, `Integrations`, `WhatsAppAnalytics` icin **compile-only `ProjectReference`** (`PrivateAssets="all"`) eklenir. Bu referanslar:

- **Sadece DLL kopyalar** — Backend kodunda hicbir `using Invekto.Appointments...` YAZILMAZ. Code review ile enforce edilir.
- **Runtime isolation korunur** — her servis hala kendi Windows servisinde calisir, kendi portunda dinler, kendi DB scope'una erisir.
- **Queue filter** worker rolu izolasyonunu surdurur: Backend `BackgroundJobServer.Queues = ["backend"]` — diger kuyruklardaki is ogeleri ilgili servisin worker'ina gider.
- **Scheduler rolu** yalnizca Backend'tedir (leader election zaten Hangfire PG advisory lock ile saglanir); scheduler due job'u kuyruga yazar, execute etmez.

**Trade-off:** Backend publish output'una ~15 MB eklenir (4 servis DLL'leri). Bu, ayri `Invekto.Scheduler` mikroservisi kurmanin maliyetine kiyasla kabul edilebilir.

**Ileride (Backlog):** Scheduler ayri bir `Invekto.Scheduler` servisine cekilebilir (queue-only, no HTTP endpoint). O zaman Backend referanslari kaldirilir, scheduler servisinin tum job referanslari olur. `tracking/roadmap.md` Backlog: "Invekto.Scheduler ayri mikroservis (G7 follow-up)".

**CLAUDE.md / INVEKTO_BASE.prompt.md mikro servis izolasyon kurali bu tek istisna icin referans verir.** Yeni servis/job eklendiginde Backend csproj'a yeni `ProjectReference` eklenmezse ayni `JobLoadException` tekrar dogar — review checklist.
