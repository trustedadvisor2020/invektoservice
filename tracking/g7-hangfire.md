# G7: Hangfire Migration

> **Durum:** IN_PROGRESS (Faz 1) | **Tarih:** 13 Nis 2026 | **Spec:** `arch/specs/g7-hangfire-migration.md`
> **Risk:** HIGH | **Topology:** PG storage + queue-per-service + strangler rollout

## Kapsam

Mevcut 10 zamanlanmis `IHostedService` (Timer + Interlocked loop) Hangfire recurring job'lara cevrilir. Her servis kendi Hangfire server'i ile calisir, ortak PG storage kullanir. Superadmin-only `/hangfire` dashboard Backend'te mount edilir.

**Out of scope:** Queue consumer worker'lar (MessageSender, AnalysisProcessing, DocumentProcessing, BenchmarkProcessing, BatchClassification) + SimulationEngine (in-memory cache cleanup).

## Faz Tablosu

| Faz | Scope | Plan JSON | Durum | Codex |
|-----|-------|-----------|-------|-------|
| Faz 1 | Altyapi + Reminder + FlowWaitResumer + Dashboard | `arch/plans/20260413-g7-hangfire-faz1.json` | DONE | FORCE_PASS iter 3 (Q) |
| Faz 2 | Waitlist + TreatmentLifecycle (Appointments) | `arch/plans/20260414-g7-hangfire-faz2.json` | DONE | FORCE_PASS iter 2 (Q) |
| Faz 3 | CronScheduler + RescueFollowUp (Automation) | `arch/plans/20260414-g7-hangfire-faz3.json` | DONE | PASS iter 1 |
| Faz 4 | TranslationCleanup + MetricsAggregation (Backend) | `arch/plans/20260414-g7-hangfire-faz4.json` | DONE | FORCE_PASS iter 1 (Q) |
| Faz 5 | OrderSync + NightlyBatch (Integrations, WhatsAppAnalytics) | - | PLANNED | - |

## Acceptance Criteria (Faz 1)

| # | Kriter | Durum |
|---|--------|-------|
| AC-1 | Hangfire NuGet + ortak `AddInvektoHangfire` extension | DONE |
| AC-2 | Migration 011: `hangfire` schema + grant | DONE |
| AC-3 | ReminderJob (Appointments, queue=appointments, cron */5min) | DONE |
| AC-4 | FlowWaitResumerJob (Automation, queue=automation, cron minutely) | DONE |
| AC-5 | Backend /hangfire dashboard + SuperAdminDashboardFilter | DONE |
| AC-6 | Eski `ReminderSchedulerService` + `FlowWaitResumerService` silinmis | DONE |
| AC-7 | Build PASS, 0 new warnings | DONE |
| AC-8 | Codex PASS iter<=2 | DONE |

## Error Codes (yeni namespace INV-JOB)

- INV-JOB-001: Hangfire storage connection failed
- INV-JOB-002: Job handler unresolved in DI
- INV-JOB-003: Recurring job registration conflict
- INV-JOB-004: Dashboard unauthorized access
- INV-JOB-005: Job execution failure (retry exhausted)

## Deliverables (Faz 1)

- [ ] Migration 011 (schema + grant)
- [ ] `src/Invekto.Shared/Hosting/HangfireSetup.cs`
- [ ] `src/Invekto.Shared/Hosting/SuperAdminDashboardFilter.cs`
- [ ] `src/Invekto.Shared/Constants/ErrorCodes.cs` (INV-JOB-001..005)
- [ ] `src/Invekto.Appointments/Services/Jobs/ReminderJob.cs` (+ Program.cs wire-up)
- [ ] `src/Invekto.Appointments/Services/ReminderSchedulerService.cs` (DELETE)
- [ ] `src/Invekto.Automation/Services/Jobs/FlowWaitResumerJob.cs` (+ Program.cs wire-up)
- [ ] `src/Invekto.Automation/Services/FlowWaitResumerService.cs` (DELETE)
- [ ] `src/Invekto.Backend/Program.cs` (Hangfire dashboard mount)
- [ ] `arch/db/migrations/011-hangfire-schema.sql`
- [ ] `arch/errors.md` (INV-JOB-xxx)
