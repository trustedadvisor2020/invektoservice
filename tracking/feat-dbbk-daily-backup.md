# FEAT-DBBK — Daily DB Backup (Hangfire)

> Status: **DONE — Codex PASS iter 4** (2026-04-18) | Owner: Q | Slug: `20260418-db-backup-hangfire`
> Risk: MEDIUM | Review: Codex 12/12 CQ + 4/4 CoVe PASS

## Problem

InvektoServices production DB'si icin **otomatik yedek aliniyor** olmadigi tespit edildi (2026-04-18).
Deploy akisi migration'i prod'da dogrudan calistirirken rollback noktasi yok. Disaster recovery
senaryosunda en son state kaybi riski %100.

## Scope

Backend icinde **Hangfire recurring job** (`backend:db-backup`) her gece calisir ve
`C:\Invekto\Backups\` altina `pg_dump --format=custom` ciktisi yazar. 14 gun ustu
dosyalar ayni job icinde silinir.

## Decisions (interview 2026-04-18)

| Konu | Karar | Gerekce |
|------|-------|---------|
| Trigger | Gunluk scheduled (Hangfire) | Pre-deploy + manuel secenekleri elendi; tek kanal yeterli |
| Scope | Full cluster, tek dosya | Per-tenant overkill (schema largely shared), schema-only yetersiz |
| Storage | Prod lokal disk `C:\Invekto\Backups\` | S3/external DR scope-out; disk 155 GB bos |
| Retention | Son 14 gun daily (tek kural) | Pre-deploy ayrimi yok |
| Mechanism | Hangfire recurring job (Backend) | Windows Scheduled Task + NSSM worker secenekleri elendi; mevcut altyapi yeniden kullanilir |
| Encryption | Hayir (pg_dump -Fc default compressed) | Fiziksel server erisimi sinirli; key-loss riski > ek guvenlik |

## Production Environment (verified 2026-04-18)

| Asset | Durum |
|-------|-------|
| pg_dump.exe | `C:\Program Files\PostgreSQL\18\bin\pg_dump.exe` (v18.2) **var** |
| `C:\Invekto\Backups\` | **YOK** — job icinde idempotent `Directory.CreateDirectory` |
| C: drive free | 155 GB (yedekli) |

## Acceptance Criteria

- **AC1:** Hangfire dashboard'da `backend:db-backup` recurring job gorunur, cron = `0 3 * * *` (konfigurasyondan degistirilebilir)
- **AC2:** Manuel "Trigger now" sonrasi `C:\Invekto\Backups\invekto-YYYYMMDD-HHmm.dump` olusur; `pg_restore --list` ile icerigi dogrulanabilir (custom format)
- **AC3:** `LastWriteTime` 14 gunden eski `*.dump` dosyalari ayni run'da silinir; **yeni dosya silinmez** (mtime safety)
- **AC4:** pg_dump exit code != 0 -> Hangfire AutomaticRetry tetiklenir + INV-JOB-010 log; job hicbir zaman "sessiz basarisiz" olmaz
- **AC5:** `appsettings.Production.json` uzerinden ayarlanir:
  - `DbBackup:Enabled` (bool, default true)
  - `DbBackup:Cron` (string, default `0 3 * * *`)
  - `DbBackup:PgDumpPath` (string, default `C:\Program Files\PostgreSQL\18\bin\pg_dump.exe`)
  - `DbBackup:OutputDir` (string, default `C:\Invekto\Backups`)
  - `DbBackup:RetentionDays` (int, default 14)
  - `DbBackup:MinFreeDiskGb` (int, default 5)
  - `DbBackup:TimeoutMinutes` (int, default 60)

## Error Codes (new)

| Code | Anlam |
|------|-------|
| INV-JOB-010 | DbBackupPgDumpFailed (exit code != 0 veya stderr anlamli) |
| INV-JOB-011 | DbBackupDiskSpaceInsufficient (< MinFreeDiskGb) |
| INV-JOB-012 | DbBackupBinaryNotFound (PgDumpPath File.Exists false) |
| INV-JOB-013 | DbBackupConfigMissing (ConnectionStrings:PostgreSQL bos/eksik) |

> Graceful cancellation (OperationCanceledException host shutdown'dan) ayri bir kod kullanmaz:
> SystemInfo log'u ile yazilir, INV-JOB-005 (retries exhausted) anlamsal olarak farklidir.

## Files

| Path | New/Modified | Purpose |
|------|--------------|---------|
| `src/Invekto.Backend/Services/Jobs/DbBackupJob.cs` | NEW | Hangfire job (pg_dump spawn + retention) |
| `src/Invekto.Backend/Program.cs` | MODIFIED | DI scope + RecurringJob.AddOrUpdate |
| `src/Invekto.Backend/appsettings.json` | MODIFIED | DbBackup defaults placeholder |
| `src/Invekto.Shared/Constants/ErrorCodes.cs` | MODIFIED | INV-JOB-010/011/012 ekleme |
| `arch/errors.md` | MODIFIED | 3 yeni kod |

## Out of Scope (intentional)

- Restore procedure runbook (ayri paket)
- External storage (S3/Azure Blob)
- At-rest encryption
- Pre-deploy backup hook (`/deploy` ile entegrasyon)
- Alerting (email/push) on failure — Hangfire dashboard + INV-JOB-010 log yeterli
- Per-tenant backup
- Backup health check endpoint

## Microservice Isolation

- **Backend-only.** `Invekto.Shared` uzerinden DTO paylasimi yok.
- `ErrorCodes.cs` Shared'a eklenir (butun servisler INV-JOB-xxx'i zaten paylasiyor — shared infra).

## Verification

1. Dev PC: `dotnet build` PASS
2. Codex review: iter 0 PASS hedefi
3. Prod deploy sonrasi manuel test: dashboard uzerinden trigger -> `.dump` dosyasi + `pg_restore --list` validation

## References

- Q preference: "DB yedegi aliyormuyuz?" (2026-04-18)
- Related lesson: arch/lessons-learned.md:577 (DataProtection key ring backup note)
- Existing Hangfire jobs: `TranslationCleanupJob`, `MetricsAggregationJob` (G7 Faz 4)
