<!-- Status: BACKLOG | 2026-04-29 -->
# FEAT-OBI — Outbound Bulk WhatsApp External Source Ingestion

> **Tarih:** 2026-04-29
> **Durum:** BACKLOG (post-pilot, kayıt aşaması)
> **Kategori:** DEV
> **Kanban:** D027 — "Yol Haritası" board (board_key='inse'), BACKLOG kolonu. Tek INSE platform genel board (Migration 039 ile board_key 'dent-pilot' → 'inse' rename). Pilot kart'larıyla aynı kanban'da, ama scope INSE-genel.
> **Öncelik:** P2 (Stage 3 sonrası, 2. müşteri öncesi)

---

## Özet

Outbound servisi (port 7107) `POST /api/v1/broadcast/send` zaten mevcut: template_id + recipients array (max 1000) + per-recipient variables + opt-out filter + status tracking + trigger engine + rate limit. Mimari [arch/contracts/outbound-broadcast.json](../arch/contracts/outbound-broadcast.json)'da.

**Eksik olan:** Recipients listesini **dış kaynaklardan otomatik çekme**. Şu an caller manuel array hazırlamalı. Q'nun talebi: INMA + Zoho öncelikli external source ingestion.

---

## Scope

### Faz 1 (MVP — INMA + Zoho)
1. **INMA Contact Pull adapter**
   - INMA `GET /api/contacts` (filtre: tag, segment, custom field) → recipient list builder
   - Phone normalization (E.164)
   - Tenant scope (her tenant kendi INMA hesabını sorgular)
2. **Zoho COQL Query adapter**
   - Zoho CRM modules (Leads, Contacts) sorgu UI
   - COQL builder: WHERE clause + field selection (phone field map)
   - OAuth refresh token re-use (mevcut FEAT-VCP/Zoho-3C pattern)
3. **Bulk Send Orchestrator**
   - External source query → recipients normalize → opt-out filter → existing `/broadcast/send` POST
   - Idempotency: `(tenant_id, source, query_hash, scheduled_at)` 24h cache
4. **Dashboard `/outbound/bulk-send` page**
   - Source picker: Manual CSV / INMA / Zoho
   - Segment builder UI (basic filter)
   - Preview (ilk 10 recipient + total count)
   - Template select + per-variable map
   - Schedule (now / scheduled_at)
   - Send button → `/broadcast/send` çağrısı, broadcast_id polling

### Faz 2 (Post-pilot, opsiyonel)
- CSV/Excel upload (file-based ingestion)
- Saved segments (named queries, re-usable)
- Recurring broadcasts (Hangfire scheduled, weekly/monthly)
- Other sources: Google Sheets, custom webhook, HubSpot

---

## Veri Modeli

```sql
-- Yeni tablo (Faz 1)
bulk_send_jobs (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  source VARCHAR(32) NOT NULL,          -- 'inma' | 'zoho' | 'csv' | 'manual'
  source_query JSONB NOT NULL,          -- {filter, fields, ...}
  template_id INTEGER NOT NULL,
  scheduled_at TIMESTAMPTZ,
  broadcast_id UUID,                    -- existing /broadcast/send response
  total_recipients INTEGER,
  status VARCHAR(20),                   -- queued|fetching|sending|completed|failed
  query_hash VARCHAR(64),               -- idempotency
  created_at TIMESTAMPTZ DEFAULT NOW(),
  completed_at TIMESTAMPTZ
);
```

---

## Bağımlılıklar

| Bağımlılık | Durum |
|------------|-------|
| Outbound `/broadcast/send` endpoint | ✅ DEPLOYED (port 7107) |
| INMA `/api/contacts` API | ❓ Kontrol gerekli (`wapcrm-marketing-api.md`) |
| Zoho COQL access | ✅ DEPLOYED (FEAT-VCP/Zoho-3C OAuth pattern) |
| Tenant opt-out registry | ✅ DEPLOYED (Outbound `/api/v1/optout/*`) |

---

## Açık Sorular (paket aktivasyonu öncesi)

1. **INMA contact pull volume cap** — tenant başına max recipient/gün?
2. **Zoho COQL field map** — phone field hep aynı mı yoksa tenant-specific mi (FEAT-TFM resolver kullan)?
3. **Bulk send WhatsApp policy guard** — 24h conversation window check, template approval verify (INMA bridge vs direct WABA)?
4. **Pricing/quota** — plan_tier'a göre bulk volume limiti (FAZ1-1 plan permission system'e ekle)?
5. **UI segment builder complexity** — sadece basit filter mi (status=interested), yoksa SQL-like advanced query mi?

---

## Aktivasyon Gate

- Pilot Stage 3 onaylanı sonrası
- 2. müşteri onboarding pre-req (Stage 3'te roadshow takvimi sonrası)
- Q öncelik onayı + paket plan JSON (interview gates 5 sorudan geçer)

---

## Referanslar

- Outbound contract: [arch/contracts/outbound-broadcast.json](../arch/contracts/outbound-broadcast.json)
- INMA marketing API: [wapcrm-marketing-api.md](../wapcrm-marketing-api.md)
- Zoho integration: [tracking/zoho-p42-oauth-scope-investigation.md](zoho-p42-oauth-scope-investigation.md)
- FEAT-TFM resolver (phone field map): [arch/features/tenant-field-mapping.md](../arch/features/tenant-field-mapping.md)
