# Dent Adavista Pilot — Master Plan

**Musteri:** Dent Adavista Dental Clinic (Kusadasi) — **INMA tenant'i** (CompanyCode: `dentadavista`)
**Amac:** Irlanda Roadshow (Dublin/Cork) lead-to-patient funnel — AI agent "Gunes" karsilama + FAQ + teklif + online consultation + post-event nurture.
**Pilot kanal:** WhatsApp + IG + Telegram (INMA uzerinden). Email YOK.

> **Onemli:** Bu pilot **generic platform feature'larinin ilk tuketicisi**. "Gunes" Dent'e ozel kod adi; platform tarafinda agent adi tenant-level ayardir. Feature'lar tenant-bagimsiz tasarlandi.

> **On Kosul:** Bu pilot **INMA-INSE Unification Platform P0** tamamlanmadan baslamaz.
> Platform projesi: `arch/platform/inma-inse-unification/`

## Yapi (yeni — 2026-04-16 refactor)

Plan iki katmana bolundu:

### 1. Generic Feature Spec'leri — `arch/features/`

Her biri tenant-bagimsiz, birden cok tenant tarafindan kullanilabilir.

| Feature | Dosya | Durum |
|---------|-------|-------|
| Welcome Template Pack | [`arch/features/welcome-template-pack.md`](../../arch/features/welcome-template-pack.md) | DRAFT |
| Multi-City Campaign | [`arch/features/multi-city-campaign.md`](../../arch/features/multi-city-campaign.md) | DRAFT |
| Lead Intake Webhook | [`arch/features/lead-intake-webhook.md`](../../arch/features/lead-intake-webhook.md) | DRAFT |
| Video Consultation Provider | [`arch/features/video-consultation-provider.md`](../../arch/features/video-consultation-provider.md) | DRAFT |
| Event Follow-Up Sequence | [`arch/features/event-followup-sequence.md`](../../arch/features/event-followup-sequence.md) | DRAFT |
| Tenant Field Mapping | [`arch/features/tenant-field-mapping.md`](../../arch/features/tenant-field-mapping.md) | DRAFT |

### 2. Dent Adavista Pilot Checklist — bu klasor

Tenant-specific configuration, icerik, go-live plan.

| Dosya | Icerik |
|-------|--------|
| [pilot-checklist.md](pilot-checklist.md) | Ana deploy checklist — 14 bolum |
| [pilot-agent-config.md](pilot-agent-config.md) | "Gunes" persona + 46 template + EN dil |
| [pilot-field-mapping.md](pilot-field-mapping.md) | INMA 10 field'in Dent semantic overlay'i |
| [pilot-golive.md](pilot-golive.md) | 20 UAT test senaryosu + asamali launch |
| [customer-info.md](customer-info.md) | Musteri bilgileri (Faz 0 toplanan) |
| [decisions.md](decisions.md) | Mimari kararlar (kilitli) |
| [phase-0-discovery.md](phase-0-discovery.md) | Ilk audit kapsami (arsiv) |
| [phase-0-audit-report.md](phase-0-audit-report.md) | Capability matrisi (arsiv) |
| [unified-platform-architecture.md](unified-platform-architecture.md) | Platform referans (`arch/platform/` altinda da var) |
| `flows.html`, `flows.pdf` | Gorsel akis dokumani |

## Kapsam Kararlari (kilitli — detay: [decisions.md](decisions.md))

| # | Konu | Deger |
|---|------|-------|
| 1 | Tarih | Esnek, kaliteye odaklan |
| 2 | Kanal v1 | WhatsApp (INMA uzerinden) |
| 3 | Landing page | Musterinin kendi sayfasi + webhook entegrasyonu |
| 4 | Online consultation | Google Meet (v1 mock, v1.1 prod OAuth) |
| 5 | Post-event nurture | DAHIL (3 stage) |
| 6 | Kanallar (v1) | INMA uzerinden WA + IG + Telegram (Email YOK) |
| 7 | Custom fields | INMA'nin 10 tenant field'i (G4 iptal) |
| 8 | Flow wait state | Persistent `flow_execution_state` (G6 — DONE) |
| 9 | Scheduler | Hangfire migration (G7 — DONE) |
| 10 | INMA-INSE unified | Native entegrasyon (UP0 + UP1) |

## Bagimlilik Sirasi

```
Unified Platform P0 (SSO + tenant sync + data + flags)
    |
    +-- Generic feature spec'leri IMPLEMENT
    |     (arch/features/*.md — pilot-bagimsiz, platform yatirimi)
    |
    +-- Pilot configuration (bu klasor)
    |     (tenant-specific icerik + UAT)
    |
    +-- Go-Live (Stage 1 -> 2 -> 3)
```

## Durum Takibi

| Alan | Durum | Not |
|------|-------|-----|
| UP0 (Unified Platform P0) | PARTIAL | UP0.1/0.1b/0.6 DONE; UP0.2/0.3/0.5 INMA-blocked (JWT public key bekliyor) |
| Feature specs (`arch/features/`) | 6 DRAFT | Implement bagimsiz paketler halinde ilerleyecek |
| Pilot checklist | READY | Feature implement tamamlandiktan sonra aktif |
| Faz 0 (Audit) | DONE | 2026-04-13 |

Detayli durum: `arch/session-memory.md` + `tracking/README.md`.

## Invekto Referanslari

- `arch/docs/microservice-guide.md` — yeni servis/modul gerekirse
- `arch/contracts/` — tenant/flow/template contract'lari
- `tracking/README.md` — master tracking (FEAT-* paket durumlari)
