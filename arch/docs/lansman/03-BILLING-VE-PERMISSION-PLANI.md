# Billing & Permission Sistemi Plani

> Son guncelleme: 2 Mart 2026
> Bagimlilik: Faz 1 (permission) → Faz 2 (billing)
> Odeme saglayici: QNB SanalPos

---

## 1. Permission Sistemi (Faz 1)

### 1.1 Mevcut Durum

| Var | Yok |
|-----|-----|
| InseFeatures[] INMA JWT'den geliyor | Backend 403 enforcement |
| Frontend nav filtering calisiyor | Feature tier ayrimi (premium/basic) |
| tenant_registry.features_json kolonu var | features_json kullanilmiyor |
| plan_tier kolonu var (baslangic/profesyonel/kurumsal) | Plan bazli kota sistemi |
| hasFeature() frontend fonksiyonu var | hasFeatureTier() fonksiyonu |

### 1.2 Hedef Yapi

**Model:** Plan = havuz, firma bazinda secim. SuperAdmin her firmanin features_json'ini ayarlar.

```
3 Katmanli Permission:

Katman 1: Plan Havuzu
  Plan, hangi ozelliklerin SECIME ACIK oldugunu belirler.
  Baslangic: WebChat, FlowBuilder(basic), Analytics(basic), Integrations(widget)
  Profesyonel: + FlowBuilder(premium), Knowledge, Outbound, Appointments, AgentAI, Analytics(premium)
  Kurumsal: + Marketing, Knowledge(premium), Outbound(premium), Integrations(premium), AgentAI(premium)

Katman 2: Firma Bazinda Secim (features_json)
  SuperAdmin, havuz icinden firma icin hangileri ACIK secimini yapar.
  Ornek: Profesyonel plandaki firma, Outbound istemeyebilir → "off"
  Plan disindaki ozellik secilemez (UI'da goruntulenmez)

Katman 3: Kota (plan bazli sabit)
  messages_per_month: 1000 | 10000 | -1
  max_users: 5 | 20 | -1
  max_flows: 3 | -1 | -1
```

### 1.3 Backend Middleware

**Yeni dosya:** `src/Invekto.Shared/Auth/FeatureGuardMiddleware.cs`

```
Her API istegi akisi:
  Request gelir
    → TenantContext'ten tenant_id al
    → tenant_registry'den plan_tier + features_json oku (cache'le)
    → Endpoint'in [RequireFeature("FlowBuilder")] attribute'unu kontrol et
    → Feature "off" ise → 403 { "error": "INV-FEATURE-DISABLED" }
    → Feature tier yetersiz ise → 403 { "error": "INV-TIER-INSUFFICIENT", "required": "premium", "current": "basic" }
    → Kota asildiysa → 429 { "error": "INV-QUOTA-EXCEEDED", "limit": 1000, "used": 1001 }
    → OK ise → devam et
```

**Attribute ornekleri:**
```
[RequireFeature("FlowBuilder")]           → FlowBuilder acik mi?
[RequireFeature("FlowBuilder", "premium")] → FlowBuilder premium mi?
[RequireQuota("messages_per_month")]       → Mesaj kotasi asildı mi?
```

### 1.4 Feature Matrisi

**Plan Havuzlari** — her plan icin SECIME ACIK degerler:

```json
{
  "baslangic": {
    "available": {
      "WebChat": ["on"],
      "FlowBuilder": ["basic"],
      "Analytics": ["basic"],
      "Integrations": ["basic"]
    },
    "quotas": { "messages_per_month": 1000, "max_users": 5, "max_flows": 3 }
  },
  "profesyonel": {
    "available": {
      "WebChat": ["on"],
      "FlowBuilder": ["basic", "premium"],
      "Knowledge": ["basic"],
      "Outbound": ["basic"],
      "Appointments": ["on"],
      "Analytics": ["basic", "premium"],
      "Integrations": ["basic"],
      "AgentAI": ["on"]
    },
    "quotas": { "messages_per_month": 10000, "max_users": 20, "max_flows": -1 }
  },
  "kurumsal": {
    "available": {
      "WebChat": ["on"],
      "FlowBuilder": ["basic", "premium"],
      "Knowledge": ["basic", "premium"],
      "Outbound": ["basic", "premium"],
      "Appointments": ["on"],
      "Analytics": ["basic", "premium"],
      "Integrations": ["basic", "premium"],
      "Marketing": ["on"],
      "AgentAI": ["on", "premium"]
    },
    "quotas": { "messages_per_month": -1, "max_users": -1, "max_flows": -1 }
  }
}
```

**Firma bazinda features_json** — SuperAdmin'in o firma icin sectikleri:

```json
// Ornek: Profesyonel plandaki "ABC E-Ticaret" firması
{
  "FlowBuilder": "premium",
  "Knowledge": "basic",
  "Outbound": "off",
  "Appointments": "on",
  "Analytics": "basic",
  "Integrations": "basic",
  "AgentAI": "on",
  "WebChat": "on"
}
```

**Kural:** features_json'daki bir deger, plan havuzunda yoksa → backend reddeder.
Ornek: Baslangic planindaki firma icin `"Knowledge": "basic"` atanamaz (havuzda yok).
```

### 1.5 Frontend Degisiklikler

**hasFeature() genisletme:**
```
hasFeature("FlowBuilder")              → true/false (acik mi)
hasFeatureTier("FlowBuilder")          → "premium" | "basic" | "off"
isFeaturePremium("FlowBuilder")        → true/false
```

**UI davranisi:**
- Feature "off" → menu'de goruntulenmez (mevcut davranis)
- Feature "basic" → goruntulenir, premium ozellikler kilitli ikon + "Upgrade" badge
- Feature "premium" → tam erisim

### 1.6 Kota Sistemi

**DB tablolari:** plan_quotas + tenant_usage (detaylar 02-LANSMAN-ROADMAP.md'de)

**Metering noktala:**
- Mesaj gonderme → Backend mesaj endpoint'i → usage_count++
- Flow olusturma → Automation servisi → flow sayisi kontrolu
- Kullanici ekleme → INMA tarafindan yonetilir (INMA'da da limit olmali)

**Kota asildikinda:**
- API: 429 Too Many Requests + aciklama
- UI: Uyari banner "Aylik mesaj limitinize ulastiniz. Plan yukseltin."

---

## 2. Billing Sistemi (Faz 2)

### 2.1 QNB SanalPos Entegrasyonu

**Gerekli islemler:**
- Tek cekim odeme (ilk abonelik)
- Recurring odeme (aylik/yillik otomatik)
- Kart saklama (tokenizasyon)
- 3D Secure (zorunlu)
- Iade / iptal
- Webhook bildirimleri

**Entegrasyon noktasi:** Backend'e yeni billing modulu (`/api/v1/billing/*`)

### 2.2 DB Semalari

**tenant_subscriptions:**

| Kolon | Tip | Aciklama |
|-------|-----|----------|
| id | SERIAL PK | |
| tenant_id | INTEGER FK | tenant_registry.tenant_id |
| plan_tier | VARCHAR(20) | baslangic, profesyonel, kurumsal |
| status | VARCHAR(20) | trial, active, past_due, cancelled, suspended |
| billing_period | VARCHAR(10) | monthly, yearly |
| amount_cents | INTEGER | Tutar (kurus cinsinden) |
| currency | VARCHAR(3) | TRY |
| current_period_start | TIMESTAMPTZ | Donem baslangici |
| current_period_end | TIMESTAMPTZ | Donem bitisi |
| trial_ends_at | TIMESTAMPTZ | Deneme suresi bitisi |
| cancelled_at | TIMESTAMPTZ | Iptal tarihi |
| qnb_customer_id | VARCHAR(100) | QNB musteri ID |
| qnb_subscription_id | VARCHAR(100) | QNB abonelik ID |
| created_at | TIMESTAMPTZ | |
| updated_at | TIMESTAMPTZ | |

**tenant_invoices:**

| Kolon | Tip | Aciklama |
|-------|-----|----------|
| id | SERIAL PK | |
| tenant_id | INTEGER | |
| subscription_id | INTEGER FK | |
| invoice_number | VARCHAR(50) UNIQUE | INV-2026-0001 formati |
| amount_cents | INTEGER | |
| currency | VARCHAR(3) | TRY |
| tax_cents | INTEGER | KDV |
| status | VARCHAR(20) | pending, paid, failed, refunded |
| pdf_url | VARCHAR(500) | |
| qnb_payment_id | VARCHAR(100) | |
| paid_at | TIMESTAMPTZ | |
| created_at | TIMESTAMPTZ | |

### 2.3 API Endpoint'leri

| Endpoint | Method | Aciklama | Kim |
|----------|--------|----------|-----|
| `/api/v1/billing/plans` | GET | Mevcut planlari listele | Herkes |
| `/api/v1/billing/subscribe` | POST | Yeni abonelik olustur (3D Secure) | Firma admin |
| `/api/v1/billing/subscription` | GET | Aktif abonelik detayi | Firma admin |
| `/api/v1/billing/change-plan` | POST | Plan degistir (upgrade/downgrade) | Firma admin |
| `/api/v1/billing/cancel` | POST | Abonelik iptal (donem sonuna kadar aktif) | Firma admin |
| `/api/v1/billing/update-card` | POST | Kart guncelle | Firma admin |
| `/api/v1/billing/invoices` | GET | Fatura listesi | Firma admin |
| `/api/v1/billing/invoices/:id/pdf` | GET | Fatura PDF indir | Firma admin |
| `/api/v1/billing/webhook` | POST | QNB odeme bildirimleri | QNB (server-to-server) |
| `/api/v1/ops/billing/overview` | GET | Tum abonelikler ozeti | SuperAdmin |
| `/api/v1/ops/billing/tenant/:id` | GET | Firma billing detayi | SuperAdmin |
| `/api/v1/ops/billing/tenant/:id/override` | POST | Manuel plan override | SuperAdmin |

### 2.4 Abonelik Yasam Dongusu

```
Firma olusturulur (INMA)
    ↓
Ilk giris → Onboarding wizard
    ↓
Plan secimi → QNB 3D Secure odeme
    ↓
status: "active", plan_tier guncellenir
    ↓
Donem sonu → QNB recurring tahsilat
    ↓
Basarili → Yeni donem baslar, fatura olusur
    ↓
Basarisiz → status: "past_due"
    ↓
3 gun sonra tekrar dene
    ↓
Hala basarisiz → 7 gun sonra status: "suspended"
    ↓
suspended → features kapanir (readonly erisim)
    ↓
Odeme yaparsa → status: "active" (otomatik acilir)
```

### 2.5 Fiyatlandirma (Ornek)

| Plan | Aylik | Yillik (aylik) | Tasarruf |
|------|-------|---------------|----------|
| Baslangic | 499 TL | 399 TL | %20 |
| Profesyonel | 1.499 TL | 1.199 TL | %20 |
| Kurumsal | 3.999 TL | 3.199 TL | %20 |

> Fiyatlar Q tarafindan belirlenir. Yukaridakiler ornek.

### 2.6 Fatura PDF

**Icerik:**
- Invekto logo + iletisim bilgileri
- Musteri bilgileri (firma adi, vergi no)
- Fatura numarasi, tarihi
- Plan detayi, donem, tutar
- KDV hesabi
- Odeme durumu

**Olusturma:** HTML template → PDF (puppeteer veya wkhtmltopdf). AI sablonu yazar.

### 2.7 Otomatik Bildirimler

| Olay | Kanal | Icerik |
|------|-------|--------|
| Odeme alindi | Email | "Odemeniz alindi. Fatura: [PDF link]" |
| Odeme basarisiz | Email + Dashboard banner | "Odemeniz basarisiz. Kart bilgilerinizi guncelleyin." |
| Plan degisti | Email | "Planiniz [eski] → [yeni] olarak guncellendi." |
| Abonelik iptal | Email | "Aboneliginiz iptal edildi. [tarih]'e kadar aktif." |
| Kota uyarisi (%80) | Dashboard banner | "Aylik mesaj limitinizin %80'ine ulastiniz." |
| Kota asildi | Dashboard banner + Email | "Mesaj limitiniz doldu. Plan yukseltin." |
| Deneme suresi bitiyor (3 gun) | Email | "Deneme sureniz 3 gun sonra bitiyor." |

---

## 3. UI Tasarimi

### 3.1 Firma Dashboard: Ayarlar > Abonelik

```
┌─────────────────────────────────────────────────┐
│ Ayarlar > Abonelik                              │
├─────────────────────────────────────────────────┤
│                                                 │
│ AKTIF PLAN: Pro                                 │
│ Donem: 1 Mart 2026 - 1 Nisan 2026             │
│ Sonraki fatura: 1 Nisan 2026 — 1.499 TL       │
│                                                 │
│ [Plan Degistir]  [Aboneligi Iptal Et]          │
│                                                 │
├─────────────────────────────────────────────────┤
│ KULLANIM                                        │
│ Mesaj: 4.521 / 10.000  ████████░░ %45         │
│ Kullanici: 8 / 20                               │
│ Flow: 12 / Sinirsiz                             │
│                                                 │
├─────────────────────────────────────────────────┤
│ ODEME YONTEMI                                   │
│ **** **** **** 4242  Son kullanma: 12/27       │
│ [Kart Guncelle]                                 │
│                                                 │
├─────────────────────────────────────────────────┤
│ FATURA GECMISI                                  │
│ INV-2026-0012  01.03.2026  1.499 TL  Odendi [PDF]│
│ INV-2026-0011  01.02.2026  1.499 TL  Odendi [PDF]│
│ INV-2026-0010  01.01.2026  1.499 TL  Odendi [PDF]│
└─────────────────────────────────────────────────┘
```

### 3.2 SuperAdmin: Billing Overview

```
┌─────────────────────────────────────────────────────────────┐
│ super.invekto.com > Billing                                 │
├─────────────────────────────────────────────────────────────┤
│ OZET                                                        │
│ Aktif abonelik: 28    MRR: 42.750 TL    Gecikmi: 3        │
├─────────────────────────────────────────────────────────────┤
│ # │ Firma          │ Plan         │ Durum  │ Sonraki Fatura │
│ 1 │ ABC Ltd        │ Profesyonel  │ Aktif  │ 15.03.2026     │
│ 2 │ XYZ Saglik     │ Kurumsal     │ Aktif  │ 01.04.2026     │
│ 3 │ 123 E-Ticaret  │ Baslangic    │ Gecikmi│ GECIKMI        │
│   │                │              │        │ [Ozellikler] [Detay] │
└─────────────────────────────────────────────────────────────┘
```

### 3.3 SuperAdmin: Firma Ozellik Yonetimi

SuperAdmin bir firmanin satirina tikladiginda, o firmanin plan havuzundaki
ozellikleri toggle edebilir:

```
┌──────────────────────────────────────────────────────────────┐
│ super.invekto.com > ABC Ltd > Ozellik Yonetimi              │
├──────────────────────────────────────────────────────────────┤
│ Plan: Profesyonel          [Plan Degistir ▾]                │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│ SAYFALAR & OZELLIKLER            DURUM       SEVIYE          │
│ ─────────────────────────────────────────────────────────    │
│ Dashboard                        Dahil       —               │
│ WebChat                          [✅ Acik]    —              │
│ Flow Builder                     [✅ Acik]    [Premium ▾]   │
│ Knowledge                        [✅ Acik]    [Basic ▾]     │
│ Outbound / Kampanya              [⬜ Kapali]  —              │
│ Appointments                     [✅ Acik]    —              │
│ Analytics                        [✅ Acik]    [Basic ▾]     │
│ Integrations                     [✅ Acik]    [Basic ▾]     │
│ AgentAI                          [✅ Acik]    —              │
│ Marketing                        Planda yok   —              │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│ KOTALAR                                                      │
│ Mesaj: 4.521 / 10.000 kullanildi                            │
│ Kullanici: 8 / 20                                            │
│ Flow: 12 / Sinirsiz                                         │
├──────────────────────────────────────────────────────────────┤
│                                          [Kaydet] [Iptal]   │
└──────────────────────────────────────────────────────────────┘
```

**"Planda yok"** satirlari: greyed-out, tiklanamaz. Plan yukseltilirse aktif olur.

---

## 4. AI Katki Ozeti

| Gorev | AI Payi | Insan Payi |
|-------|---------|-----------|
| Permission middleware kodu | %90 (Claude Code yazar) | %10 (Q review) |
| DB migration SQL | %95 | %5 (Q calistirir) |
| QNB entegrasyon kodu | %70 (AI yazar, QNB API dokumani gerekli) | %30 (test + debug) |
| Billing UI sayfalari | %85 | %15 (Q UX review) |
| Fatura PDF sablonu | %95 | %5 |
| Email bildirimleri | %90 | %10 |
| Feature matris tanimlamalari | %20 (AI onerir) | %80 (Q karar verir) |
| Fiyatlandirma | %0 | %100 (is karari) |
