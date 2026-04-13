# Dent Adavista Pilot — Master Plan

**Müşteri:** Dent Adavista Dental Clinic (Kuşadası) — **INMA tenant'ı** (CompanyCode: `dentadavista`)
**Amaç:** İrlanda Roadshow (Dublin/Cork) lead-to-patient funnel — AI agent "Güneş" karşılama + FAQ + teklif + online consultation + post-event nurture.
**Pilot kanal:** WhatsApp + IG + Telegram (INMA üzerinden, hepsi hazır). Email YOK.

> **🔑 Ön Koşul:** Bu pilot **INMA↔INSE Unification Platform P0** tamamlanmadan başlamaz.
> **Platform projesi:** `C:/CRMs/InvektoServices/arch/platform/inma-inse-unification/` (ayrı proje)
> **Eski mimari notu (referans):** [unified-platform-architecture.md](unified-platform-architecture.md) → artık `arch/platform/` altına taşındı.

## Kapsam Kararları (kilitli)

| # | Karar | Değer |
|---|-------|-------|
| 1 | Tarih | ESNEK — deadline sabit değil, kaliteye odaklan |
| 2 | Kanal v1 | WhatsApp (sadece) |
| 3 | Landing page | Müşterinin kendi landing page'i kullanılacak — biz webhook/form entegrasyonu yapacağız |
| 4 | Online consultation | Google Meet (v1 MOCK) |
| 5 | Post-roadshow nurture | DAHİL (Faz 8) |
| 6 | Kanallar (v1) | INMA üzerinden **WA + IG + Telegram** (Email YOK) |
| 7 | Custom fields | **INMA'nın mevcut 10 tenant field'ı kullanılacak** (G4 iptal) |
| 8 | Flow wait state | Persistent `flow_execution_state` tablo (G6) |
| 9 | Scheduler | **Hangfire migration** (G7 — platform yatırımı) |
| 10 | INMA-INSE unified platform | **Native entegrasyon** — SSO + unified tenant + shared data + embedded UI (P0 + P1 maddeleri) |

## Faz Haritası

| Faz | Dosya | Süre | Bağımlılık |
|-----|-------|------|-----------|
| 0 | [phase-0-discovery.md](phase-0-discovery.md) | 0.5g | — |
| 1 | [phase-1-tenant-setup.md](phase-1-tenant-setup.md) | 0.5g | Faz 0 |
| 2 | [phase-2-whatsapp-connector.md](phase-2-whatsapp-connector.md) | 1g | Faz 1 |
| 3 | [phase-3-ai-agent-gunes.md](phase-3-ai-agent-gunes.md) | 1.5g | Faz 1 |
| 4 | [phase-4-lead-intake.md](phase-4-lead-intake.md) | 1g | Faz 2 |
| 5 | [phase-5-flow-builder.md](phase-5-flow-builder.md) | 1.5g | Faz 3, 4 |
| 6 | [phase-6-offer-appointment.md](phase-6-offer-appointment.md) | 1g | Faz 5 |
| 7 | [phase-7-online-consultation.md](phase-7-online-consultation.md) | 0.5g | Faz 6 |
| 8 | [phase-8-post-roadshow-nurture.md](phase-8-post-roadshow-nurture.md) | 0.5g | Faz 5 |
| 9 | [phase-9-uat-golive.md](phase-9-uat-golive.md) | 1g | Hepsi |

**Toplam:** ~9g (pilot) + ~11-15g (INSE gap fix, G4 iptal) + ~14-19g (Unified P0 + domain) = **~34-43 iş günü**.

Detay: [decisions.md](decisions.md) · [unified-platform-architecture.md](unified-platform-architecture.md)

**Öncelik sırası:**
1. **Unified platform P0 (ŞART):** SSO + unified tenant + shared data + feature flags + contract discipline (~12-15g)
2. **INSE gap fix (paralel):** G3 · G7 · G4 · G6 (~15-20g)
3. **Pilot Faz 1→9** (platform + INSE hazır olduktan sonra)
4. **Unified P1 (UX iyileştirme):** Embedded UI · feature surfacing · shared domain (v1.1)

## Durum Takibi

| Faz | Durum | Başlangıç | Bitiş | Notlar |
|-----|-------|-----------|-------|--------|
| UP0 | ⏳ Unified Platform P0 (SSO+tenant+data+flags) | — | — | [unified-arch](unified-platform-architecture.md) |
| 0 | ✅ Tamam (audit + kararlar + INMA Swagger + INMA webhook doğrulandı) | 2026-04-13 | 2026-04-13 | [audit](phase-0-audit-report.md) · [kararlar](decisions.md) |
| 1 | ⏳ Bekliyor | — | — | — |
| 2 | ⏳ Bekliyor | — | — | — |
| 3 | ⏳ Bekliyor | — | — | — |
| 4 | ⏳ Bekliyor | — | — | — |
| 5 | ⏳ Bekliyor | — | — | — |
| 6 | ⏳ Bekliyor | — | — | — |
| 7 | ⏳ Bekliyor | — | — | — |
| 8 | ⏳ Bekliyor | — | — | — |
| 9 | ⏳ Bekliyor | — | — | — |

Semboller: ⏳ bekliyor · 🔵 devam · ✅ tamam · ⚠️ blocked

## Kaynak Dosyalar
- `../Flowchart.pdf` — müşteri akış şeması (orijinal)
- `../ROADSHOW Aİ AGENT KARŞILAMA MESAJI.docx` — 10 karşılama varyantı + 12 FAQ

## Invekto Referansları
- `arch/docs/microservice-guide.md` — yeni servis/modül gerekirse
- `arch/contracts/` — tenant/flow/template contract'ları
- `tracking/README.md` — master tracking (pilot tamamlanınca buraya taşınacak)
