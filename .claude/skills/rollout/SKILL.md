---
name: rollout
description: >
  Schema migration + multi-tenant backfill rollout. Runs DB migrations against
  single shared Postgres, backfills existing tenants (tenant_id scoped), and
  verifies per-service integration. One tenant failure does not block the rest.
  Use when user says "rollout", "migration", "schema change", "backfill",
  "tum tenantlara uygula", "multi-tenant", "toplu migration", "feature rollout".
argument-hint: "<feature-description> [--dry-run] [--skip-backfill]"
---

# Rollout — Multi-Tenant Migration Rollout

InvektoServices tek Postgres DB uzerinde schema migration + mevcut tenant'lar icin backfill.
Bir tenant'in backfill hatasi digerlerini durdurmaz.

## InvektoServices Tenant Modeli

**ONEMLI:** InvektoServices tek Postgres DB kullanir. Her tablo `tenant_id INTEGER NOT NULL REFERENCES tenant_registry(tenant_id)` icerir. Tenant'lar INMA'dan gelir, InvektoServices olusturmaz/silmez.

TONIVA'daki "per-tenant database" modeli YOK. Rollout burada:
1. Tek Postgres'e migration uygula (idempotent)
2. Mevcut tenant'lar icin satir-bazli backfill gerekiyorsa uygula
3. Etkilenen servisi (veya servisleri) rebuild + restart

## Usage

```
/rollout Add can_download_reports column to users table
/rollout --dry-run Check which tenants need sentiment_enabled default
/rollout --skip-backfill Just apply schema, no tenant data backfill
```

## Pipeline

### Phase 1: Preparation
1. Ilgili `arch/db/{service}.sql` dosyasini oku — schema source of truth
2. Migration SQL'i olustur (`arch/db/migrations/YYYYMMDD-feature.sql`)
3. `IF NOT EXISTS` / `DO $$ BEGIN ... EXCEPTION ... END $$` pattern ile idempotent yaz
4. Hangi servis(ler) etkileniyor belirle (backend, chatanalysis, vs.)

### Phase 2: Pre-flight Check
1. Production Postgres'te hedef tablo/kolon durumunu kontrol et
2. Tenant listesini al: `SELECT tenant_id, name FROM tenant_registry WHERE active = true`
3. Hangi tenant'lar backfill gerektirir belirle
4. `--dry-run` ise burada dur, rapor ver

### Phase 3: Schema Migration
1. Migration SQL'i calistir (tek Postgres)
2. `\d {table}` ile dogrula
3. Idempotent oldugu icin zaten varsa skip

### Phase 4: Tenant Backfill (gerekliyse)
Her tenant icin sirayla:
1. `INSERT`/`UPDATE ... WHERE tenant_id = @tid` ile default degerleri uygula
2. Dogrula — kayit sayisi beklenen mi
3. **Hata durumunda** — log'a yaz, **sonraki tenant'a gec** (durma!)

`--skip-backfill` varsa bu fazi atla.

### Phase 5: Service Restart
1. Etkilenen servis(ler)i tespit et (backend, chatanalysis, vs.)
2. `/deploy {service}` ile publish + NSSM restart
3. Health check: `mcp__invekto-ops__server-health`

### Phase 6: Report
```
ROLLOUT REPORT — [feature-description]
──────────────────────────────────────
Migration SQL:    arch/db/migrations/20260411-add-feature.sql
Services affected: Backend, ChatAnalysis
Schema applied:   OK (idempotent, no change needed)

TENANT BACKFILL:
Total tenants:    23
Already OK:       15
Backfilled:        8
Failed:            0

DETAIL:
ID  | Tenant       | Status
----|--------------|--------
42  | Acme Corp    | OK (already had default)
43  | Beta Ltd     | BACKFILLED (3 rows)
44  | Gamma Inc    | BACKFILLED (1 row)
...

SERVICES:
- Invekto.Backend       : RESTARTED, health OK
- Invekto.ChatAnalysis  : RESTARTED, health OK
```

## Critical Rules

- **Source of truth:** `arch/db/{service}.sql` — migration SQL bu dosyaya hizali olmali
- **Idempotent:** Her migration `IF NOT EXISTS` / `DO $$ BEGIN ... EXCEPTION WHEN duplicate_column ... END` ile yazilmali
- **Tenant izolasyonu:** Backfill `WHERE tenant_id = @tid` ile SCOPE'LU, **global UPDATE YASAK**
- **Cross-tenant leak riski:** `tenant_registry` haricinde hicbir sorgu `WHERE tenant_id = ...` olmadan calismamali
- **Izolasyon:** Bir tenant FAIL olursa digerlerine devam et
- **snake_case:** Tum kolon adlari snake_case (hook enforce eder)
- **Shared DTO:** Schema degisikligi Shared DTO'yu etkiliyorsa service-isolation-checker ile dogrula
- **Dry-run oncelikli:** Ilk calistirmada `--dry-run` ile ne olacagini goster, Q onaylasin
- **Single DB:** Tek Postgres — TONIVA'daki per-tenant DB modeli **YOK**

## Auto-Fix Patterns

| Hata | Otomatik Fix |
|------|-------------|
| Kolon zaten var | SKIP (idempotent) |
| Tenant'in etkilenmesi beklenen satir yok | SKIP, raporda belirt (yeni tenant) |
| Veri tipi uyusmazligi | DUR, Q'ya sor |
| Permission denied | DUR, Q'ya sor |
| Shared DTO breaking change tespit edildi | DUR, tuketici servisleri listele, Q'ya sor |
| Service restart fail | DUR, `/deploy` prosedurunu manuel calistir |

## Multi-Service Impact

Eger migration birden fazla servisi etkiliyorsa:
1. Her servisin DTO/entity'sini guncelle
2. `Invekto.Shared/DTOs/` uzerinden ortak kontrat guncelle (varsa)
3. Tum etkilenen servisleri ayni deploy turunda restart et
4. `service-isolation-checker` agent'i ile cross-service referans kontrolu

## Referans

- `arch/db/migrations/README.md` — Migration dosyalarinin genel kurallari
- `arch/tenant-isolation.md` — Tenant isolation canonical
- `.claude/commands/deploy.md` — Production deploy prosedurunun tam metni
- `arch/db/{service}.sql` — Per-service schema source of truth
