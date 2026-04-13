# INMA + INSE — Unified Platform Mimarisi

## Teknik Topoloji (2026-04-13 doğrulandı)

| Katman | Stack | DB | Sunucu | Ekip |
|--------|-------|-----|--------|------|
| **INMA** | Angular (frontend) + (backend TBD) | **SQL Server** (ayrı sunucu, ayrı şema) | Ayrı server | Ayrı dev ekibi |
| **INSE** | React 18 + .NET 8 | **PostgreSQL 16** + pgvector | Invekto production | Q + Claude |
| **Köprü** | REST (INSE → INMA API, auth `X-CIB-SecretKey`) + Webhook (INMA → INSE, per-tenant config) | — | — | — |

**Sonuç:** Farklı DB + farklı repo + farklı ekip. Shared DB mümkün DEĞİL. Entegrasyon **API + webhook + SSO** katmanında.

**Koordinasyon:** INMA'da mantıklı olduğu sürece değişiklik yapılabilir — ama *arka plan review*: her INMA refactor'u INMA ekibiyle kararlaştırılacak.

## Paradigm Shift (2026-04-13)

**ÖNCEKİ ANLAYIŞ (YANLIŞ):**
> INMA bir 3rd-party gateway; INSE onun üzerine entegrasyon yapıyor.

**DOĞRU ANLAYIŞ:**
> INMA (5 yıllık mesajlaşma platformu) + INSE (Invekto Services — AI/flow/analitik katmanı) **tek sahibin iki sistemi**. Tek bir native ürün gibi davranmalılar.
>
> - **Firmalar = INMA müşterileri (= tenants)**
> - **Kullanıcılar INMA'ya login olur**
> - **INSE servisleri INMA'nın eksiklerini tamamlıyor** (AI agent, intent, flow builder, post-nurture, custom fields, Hangfire, rotation, persistent state)
> - **Kullanıcı INSE'nin INMA'dan ayrı olduğunu HİSSETMEMELİ**

## Dent Adavista Bu Bağlamda
Dent Adavista → **INMA tenant'ı**, `CompanyCode` = `dentadavista`. INSE bu tenant'a AI agent + flow + nurture katmanı ekliyor. Tek login, tek UI, tek data.

---

## Native Entegrasyon İçin Yapılması Gerekenler

Mevcutta INMA ↔ INSE arası iletişim **API çağrısı + webhook** ile — iki sistem gibi duruyor. Aşağıdakiler olmazsa kullanıcı "iki ayrı uygulama" hissi alır.

### 0. 🧭 Domain & UX Stratejisi (KARAR: aynı domain + aynı feel)
- **Tek domain:** `app.invekto.com` (ya da müşteri-facing: INMA'nın mevcut domain'i)
- **Path-based routing:** INMA core `/`, INSE widget/panels `/ai/*`, `/flows/*`, `/ai-inbox/*`
- **Reverse proxy:** nginx/Caddy — INMA Angular + INSE React aynı origin'den serve edilir
- **Shared design system:** Angular ↔ React farkı kullanıcıya yansımasın:
  - Ortak CSS custom properties (renk, font, spacing)
  - Ortak icon set
  - Ortak component guidelines (button, modal, form)
- **Nav entegrasyonu:** INMA sidebar'ına INSE menü girdileri (link'ler `/ai/...` path'lerine)
- **Embedded mode:** INSE React bileşenleri INMA Angular içine `<iframe>` ile başlayıp, v2'de **Web Components** ile native embed (Angular + React interop için temiz yol)

**Efor:** M (4-5g) — reverse proxy + design token sync + nav entries

### 1. 🔐 Unified Identity (SSO)
**Problem:** INMA'da login, INSE'ye ayrı login mi?
**Çözüm:**
- INMA JWT/session token → INSE otomatik kabul eder
- INSE kendi kullanıcı tablosunu kaldırır, **INMA users = source of truth**
- INSE API'leri `Authorization: Bearer <inma-jwt>` kabul etsin
- Token içinde `companyCode` + `userId` + `role` claim'leri
- Logout: INMA logout → INSE session de biter

**Efor:** M (3-4g) — INSE auth middleware refactor

### 2. 🏢 Unified Tenant Model
**Problem:** INMA'da `CompanyCode`, INSE'de `tenant_id` — iki ayrı ID.
**Çözüm:**
- `CompanyCode` = `tenant_id` (1:1). Tüm INSE tablolarında `tenant_id VARCHAR` = INMA'nın CompanyCode'u
- Yeni tenant provisioning: INMA'da firma yaratıldığında INSE'de otomatik tenant row açılır (event-driven)
- **Memory kuralı güncellemesi:** "INMA MSSQL READONLY" → **company/user read-only**, ama **message read/write + webhook event YAZMA** açık

**Efor:** S (1-2g) — migration + webhook hook

### 3. 🎨 Embedded UI / Single Navigation
**Problem:** INMA UI ayrı, INSE UI ayrı domain/port.
**Seçenek A — Iframe embed:** INMA içinde INSE ekranları iframe ile (hızlı, UX ortalama)
**Seçenek B — Micro-frontend (Module Federation):** INSE React bileşenleri INMA shell'ine runtime import (temiz, orta efor)
**Seçenek C — Reverse proxy + shared shell:** Tek domain, path-based (`app.invekto.com/chats` INMA, `/ai-flow` INSE), ortak header/sidebar (en native his, en uzun efor)

**Önerim:** **C** (uzun vadeli) ama pilot için **A** hızlı start.

**Efor:** A=S(2g) · B=M(5g) · C=L(10g)

### 4. 🔄 Bidirectional Real-Time Sync
**Problem:** Şu an webhook tek yönlü (INMA → INSE). INSE'de flow cevap üretince geri INMA'ya API çağrısı. 2 hop = latency + hata riski.
**Çözüm:**
- Webhook IN: INMA her mesajda INSE webhook'a push (screenshot'taki özellik — zaten var)
- API OUT: INSE `start-chat-v3` ile yanıt gönderir
- **+EK:** Shared event bus (Postgres LISTEN/NOTIFY ya da Redis Streams) — tek tabloda mesaj history, her iki sistem aynı storage'a yazar (v2)
- **Read-your-writes:** Agent cevabı gönderdiği anda INMA UI'da anlık görünsün (WebSocket push)

**Efor:** S (webhook zaten var) + M (shared bus v2)

### 5. 📊 Shared Data Layer (Lead / Conversation / Contact)
**Problem:** Farklı DB'ler (SQL Server vs PostgreSQL). Tek DB imkansız.
**Çözüm (distributed source of truth):**
- **Contacts/Phone numbers:** INMA SQL Server (source of truth, 5 yıllık)
- **Conversations/Messages:** INMA SQL Server
- **AI-enriched data:** INSE PostgreSQL
  - `contact_ref` tablosu: `{ tenant_id, inma_contact_id, inse_contact_id, synced_at }`
  - `flow_state`, `lead_score`, `intent_history`, `offer_status` — INSE'de kalır
- INSE API'leri INMA'nın read API'sinden contact bilgisini **cache'li** çeker
- Write path: INSE contact üzerinde bir custom field güncellerken INMA API'ye write

**G4 Custom Fields — REVİZE (2026-04-13):**
> INMA tenant bazlı **zaten 10 custom field** destekliyor (var olan feature). **Yeniden tasarım yapmayız, INMA'nın mevcut 10 field'ını kullanırız.**
>
> - Dent Adavista için 10 field map'lenecek: `roadshow_city`, `appointment_slot`, `offer_status`, `deposit_status`, `flight_booked`, `documents_complete`, `xray_uploaded`, `xray_file_id`, `meet_link`, `nurture_stage` = tam 10, sığıyor ✅
> - INSE bu field'ları INMA API üzerinden okur/yazar
> - **G4 paketi iptal** — 4-5g kazanıldı 🎉
> - Eğer ileride 10'dan fazla lazım olursa: INMA'nın field kapasitesini 20'ye çıkarma talebi (INMA ekibine mini paket)

**Efor:** M (3-4g) — INMA custom field API contract + INSE adapter + Dent field mapping

### 6. 🧩 Native Feature Surfacing (INMA UI içinde INSE özellikleri)
INMA sohbet ekranında kullanıcı şunları **ayrı sekmeye gitmeden** görmeli/kullanmalı:
- [ ] Lead detayında **AI Intent** etiketi (INSE üretir, INMA gösterir)
- [ ] Sohbet kutusunun yanında **"AI Suggest Reply"** butonu (INSE template varyantından öner)
- [ ] Sohbet üstbilgisi: **flow state** ("Nurture Day 3/14", "Offer sent, awaiting reply")
- [ ] Contact paneli: **custom fields** (offer_status, roadshow_city, xray_uploaded)
- [ ] Sidebar'a yeni tab: **"Flows"** (INSE flow builder)
- [ ] Template modal: INMA quick-reply + INSE AI variant rotation birleşik

**Efor:** M (4-5g) — INMA UI extension points + INSE widget API

### 7. ⚙️ Unified Admin & Configuration
**Problem:** INSE admin paneli nerede — ayrı mı?
**Çözüm:**
- INMA sidebar'a "AI & Automation" kategorisi: Flows, Templates, AI Agents, Custom Fields, Reports
- INSE'nin ayrı dashboard'u YOK — her şey INMA admin menüsünden
- Permission: INMA user role'lerine INSE permission'ları map'lenir (`admin` → full INSE, `agent` → read flows)

**Efor:** S (2g) — INMA menu config + INSE permission matrix

### 8. 📜 Shared Audit & Notification
- Her iki sistemin log'u tek audit trail'de (kim ne yaptı)
- Notification center: "Offer sent by AI", "Flow completed", "X-ray uploaded" — INMA notification bell'inde
- **Efor:** S (2g)

### 9. 💰 License / Feature Flag Merkezi (INMA)
- INMA'da firma lisansı = INSE feature flag'leri
- Örn: `ai_agent_enabled`, `flow_builder_enabled`, `max_custom_fields=50`
- INSE her request başında INMA'dan feature check (cache'li)
- **Efor:** S (1-2g)

### 10. 🌐 Shared Domain & Cookies
- Tek domain: `app.invekto.com`
- INMA: `/` (root), INSE: `/ai/*` path
- Cookie shared, CORS sorunsuz, "iki uygulama" hissi yok
- **Efor:** M (3g) — reverse proxy (nginx/Caddy) + cookie domain ayarı

### 11. 🔔 Unified Real-Time (WebSocket)
- Tek WebSocket bağlantısı, tüm event'ler (mesaj + flow + AI intent)
- Şu an: INMA kendi WS'i, INSE ayrı olabilir. Birleştir
- **Efor:** M (3-4g)

### 12. 🚀 Shared Deployment Discipline
- INMA ve INSE aynı release cycle'da değil (farklı repo/teams olabilir) — ama breaking contract kontrolü şart
- Shared DTO contracts `Invekto.Shared` içinde (INSE tarafı zaten yapıyor)
- **Ek:** INMA tarafında da `Invekto.Shared` referansı ya da contract test suite
- **Efor:** S (1g)

---

## Efor Özeti (Native Entegrasyon)

| # | Madde | Efor | Öncelik |
|---|-------|------|---------|
| 1 | SSO | M (3-4g) | 🔴 P0 (Dent pilot için şart) |
| 2 | Unified tenant | S (1-2g) | 🔴 P0 |
| 3 | Embedded UI | A=S / C=L | 🟡 P1 (pilot için A, v2 için C) |
| 4 | Bi-directional sync | S + M | 🔴 P0 (webhook var, shared bus v2) |
| 5 | Shared data layer | M (3-4g) | 🔴 P0 |
| 6 | Feature surfacing | M (4-5g) | 🟡 P1 |
| 7 | Unified admin | S (2g) | 🟡 P1 |
| 8 | Audit/notification | S (2g) | 🟢 P2 |
| 9 | License/feature flag | S (1-2g) | 🔴 P0 |
| 10 | Shared domain | M (3g) | 🟡 P1 |
| 11 | Unified WebSocket | M (3-4g) | 🟢 P2 |
| 12 | Contract discipline | S (1g) | 🔴 P0 |

**P0 minimum:** 0+1+2+4+5+9+12 = **~14-19g** (Dent pilot için şart)
**P1 ekle:** +3A+6+7 = **~19-24g**
**P2 ekle:** +8+11 = **~24-30g**

> **Not:** Madde 10 "shared domain" → Madde 0'a absorbe edildi (P0'da).
> **Not:** G4 iptal sayesinde INSE tarafında 4-5g kazanıldı.

---

## Mevcut Duruma Etki (Önceki Planın Revizesi)

| Önceki Gap | Unified Perspektif Sonrası |
|-----------|--------------------------|
| G2 Multi-channel adapter | Değişmedi — INSE tarafında INMA API adapter |
| G4 Custom fields system-wide | **Güçlendi** — INMA tüm tenantlar için faydalanacak, ortak JSONB |
| G6 Flow state persistence | Değişmedi — INSE'de kalır |
| G7 Hangfire | **Değişmedi ama INMA da faydalanabilir** (INMA'nın kendi scheduler'ı varsa birleştir) |
| Faz 1 Tenant provisioning | **SİMDİ: INMA'da firma aç → INSE auto-provision** (yeni akış) |
| Faz 3 AI Agent | INSE'de kalır, ama UI surface INMA sohbet ekranında |
| Faz 5 Flow | INSE'de kalır, state INMA conversation'a link |
| Faz 7 Google Meet | INSE'de, ama event notification INMA'da |

## Sıradaki Karar Noktaları

1. **Single domain mi, iki domain + SSO mu?** (Madde 10)
2. **Embedded UI pattern:** Iframe / Module Federation / Reverse proxy? (Madde 3)
3. **Data ownership sınırı:** Lead/Contact tamamen INMA'da mı, INSE extension tablosu mu?
4. **Deployment:** Aynı release pipeline mi, bağımsız mı?
5. **INMA refactor bütçesi:** INMA 5 yıllık, dokunulması zor olabilir. SSO + webhook + menu entry dışında ne kadar derin müdahale? Q kararı.
