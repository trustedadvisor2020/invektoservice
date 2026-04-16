# SPEC: Event Follow-Up Sequence

> **Spec ID:** FEAT-EFS | **Paket:** TBD | **Risk:** LOW
> **Yazar:** Q | **Son Guncelleme:** 2026-04-16 | **Durum:** DRAFT

## 1. Intent (Ne & Neden)

Event tabanli (roadshow, seminer, campaign) sonrasi "henuz karar vermedim" / "dusunuyorum" / "cevap yok" lead'leri icin N-asamali drip sequence. Amac: lead-to-customer conversion artirmak (iddia: 2-3x), lead verisini kaybetmemek, tenant'a uzun vadeli nurture yetenegi kazandirmak.

Generic: tenant kendi asama sayisini + gun araliklarini + mesaj icerigini belirler. Pilot ornek: Dent Adavista 3-stage (Day 3 / Day 7 / Day 14) "post-roadshow nurture".

## 2. Acceptance Criteria

| # | Kriter | Dogrulama |
|---|--------|-----------|
| AC-1 | Tenant `followup_sequence_config` JSONB (stages: `[{ delay_days, template_slug, template_group }]`) | UI editor |
| AC-2 | Warm pool entry 4 tetikleyici: (a) no-reply welcome chain, (b) offer declined, (c) offer timeout, (d) offer on_hold N gun | Flow execution log kaniti |
| AC-3 | Drip job Hangfire scheduled — her stage ayri `BackgroundJob.Schedule` | Hangfire dashboard |
| AC-4 | Exit conditions: lead reply / STOP / booking success | Stage skip + audit log |
| AC-5 | A/B control group (optional, tenant flag) — %X lead'e gonderim SKIP | Comparison report endpoint |
| AC-6 | Opt-out footer her drip mesajda (template substitution variable) | Template render snapshot |
| AC-7 | Analytics: open/reply rate per stage | `daily_metrics` yeni metric key |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| Hangfire `BackgroundJob.Schedule` (per-lead-per-stage) vs `RecurringJob` (batch scanner) | Lead ozel timing onemli; batch scanner 1-saatlik drift acar | CQ6 N+1 risk yok — job count bounded per lead |
| Opt-out check drip job icinde (cron guard degil) | Stateless job, lead state an icinde check | — |
| Config JSONB (tenant_settings) vs dedicated table | Tenant nadir degisir, JSON yeterli | — |
| A/B group assignment deterministic (hash + seed) | Reproducible | Mirror G3 pattern |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| Sequence config schema | `arch/contracts/followup-sequence.json` (yeni) |
| DB Schema | `arch/db/marketing.sql` veya `arch/db/pkt6b1.sql` — `leads.followup_state JSONB`, `leads.followup_ab_group` ALTER |
| Error Codes | INV-MK-050 (sequence_config_invalid), INV-MK-051 (stage_not_found), INV-MK-052 (followup_opt_out) |

## 5. Scope Boundaries

### In Scope
- `tenant_settings.followup_sequence_config` JSONB + validation
- Warm pool entry 4 tetikleyici hook (flow engine + offer state machine)
- Per-lead-per-stage Hangfire job
- Exit condition checks (reply / STOP / booking)
- Dashboard sequence editor UI
- A/B group assignment + metric counter
- Opt-out footer template helper

### Out of Scope (Explicit)
- Dynamic template generation (LLM-generated message per lead) — v2
- Multi-sequence per lead (tek sequence aktif anda)
- Cross-tenant sequence library (v2)
- Email/SMS kanali (v1 sadece mevcut channel — WA/IG/Telegram via INMA)

### Degismeyen Alanlar (Pre-existing)
- Hangfire setup (G7)
- `daily_metrics` kolonlari
- Offer state machine (see `tenant-field-mapping.md`)
- Template substitution

## 6. Service Boundaries

| Servis | Rol | Degisiklik |
|--------|-----|-----------|
| Marketing | Sequence orchestrator + Hangfire job handler | Yeni service |
| Backend | Config CRUD proxy | Yeni endpoint |
| Automation | Warm pool entry hooks (trigger events) | Minor |
| Dashboard | Sequence editor | Yeni page |

## 7. Risk & Mitigation

| Risk | Olasilik | Mitigation |
|------|----------|-----------|
| Opt-out race: lead STOP attiktan sonra scheduled job hala calisir | MEDIUM | Job execution basinda opt_out + STOP keyword check |
| Hangfire job backlog 1000+ lead | LOW | Job volume metric + queue isolation (`marketing-followup` queue) |
| Tenant 10-stage sequence tanimlarsa spam | MEDIUM | Validation: max 5 stage, max 30 gun total window |
| A/B group split bias (cok az lead, istatistik yetersiz) | MEDIUM | Min sample size warning UI |

## 8. Pilot Consumer

Dent Adavista — 3-stage post-roadshow nurture (Day 3 / 7 / 14), tetikleyici 4 (a/b/c/d hepsi), A/B %50/%50. Detay: `DentAdavista/plan/pilot-checklist.md`.
