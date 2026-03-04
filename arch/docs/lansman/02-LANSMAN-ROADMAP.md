# Invekto SaaS Lansman Roadmap

> Son guncelleme: 2 Mart 2026
> Toplam sure: ~10 hafta (Faz 1-4)
> Prensip: Minimum insan yuku, maksimum AI otomasyon

---

## Genel Bakis

| Faz | Ad | Sure | Sonuc |
|-----|----|------|-------|
| **Faz 0** | Karar & Hazirlik | Hafta 1 | Marka, domain, mimari kararlar kesinlesir |
| **Faz 1** | Guvenlik Temeli | Hafta 2-3 | Backend permission enforcement, feature tiering |
| **Faz 2** | Para Alma | Hafta 4-6 | QNB SanalPos, billing UI, fatura |
| **Faz 3** | Musteri Deneyimi | Hafta 7-8 | Onboarding wizard, dokumantasyon, self-service |
| **Faz 4** | Lansman | Hafta 9-10 | Ilk musteriler, feedback dongusu |
| **Faz 5** | Buyume (surekli) | Hafta 11+ | AI otomasyon, icerik, optimizasyon |

---

## FAZ 0: Karar & Hazirlik (Hafta 1)

**Amac:** Belirsizlikleri ortadan kaldir, herkes ayni sayfada olsun.

| # | Gorev | Kim | Cikti | Durum |
|---|-------|-----|-------|-------|
| 0.1 | Marka netlesir: **Invekto** (TONIVA ayri dunya) | Q | Karar | ✅ TAMAM |
| 0.2 | Domain plani kesinlesir | Q | `super.invekto.com` = ops, `crm.invekto.com` = firma | ✅ TAMAM |
| 0.3 | Plan tierlari tanimla | Q | Baslangic / Profesyonel / Kurumsal | ✅ TAMAM |
| 0.4 | QNB SanalPos API erisimi | Q | API credentials verilecek | ⏳ BEKLIYOR |
| 0.5 | Takim rolleri ve sorumluluklar netlesir | Q | Bu dokumandaki rol dagalimi | ✅ TAMAM |

### Karar: Plan Tierlari

**Model:** Her plan bir **havuz** tanimlar. Havuzdaki sayfa, ozellik ve entegrasyonlar firma bazinda acilip kapatilabilir (lisanslama). Plan yukseldikce havuz genisler.

#### Tier Yapisi

| | Baslangic | Profesyonel | Kurumsal |
|--|-----------|-------------|----------|
| **Aciklama** | Kucuk isletmeler | Buyuyen isletmeler | Tam kontrol |
| **Mesaj limiti / ay** | 1.000 | 10.000 | Sinirsiz |
| **Kullanici sayisi** | 5 | 20 | Sinirsiz |

#### Sayfa / Ozellik Havuzu (Plan bazinda SECIME ACIK)

Her satir, o plan seviyesinde firma icin **acilabilir veya kapali tutulabilir**.
SuperAdmin (super.invekto.com) firma bazinda toggle yapar.

| Ozellik | Baslangic | Profesyonel | Kurumsal |
|---------|-----------|-------------|----------|
| Dashboard | Dahil (her zaman) | Dahil | Dahil |
| WebChat | Secilebilir | Secilebilir | Secilebilir |
| Flow Builder — basic | Secilebilir (max 3) | Secilebilir | Secilebilir |
| Flow Builder — premium | - | Secilebilir | Secilebilir |
| Knowledge — basic | - | Secilebilir | Secilebilir |
| Knowledge — premium | - | - | Secilebilir |
| Outbound / Kampanya — basic | - | Secilebilir | Secilebilir |
| Outbound / Kampanya — premium | - | - | Secilebilir |
| Appointments | - | Secilebilir | Secilebilir |
| Analytics — basic | Secilebilir | Secilebilir | Secilebilir |
| Analytics — premium / RI | - | Secilebilir | Secilebilir |
| Integrations — basic | Secilebilir (widget) | Secilebilir (3 adet) | Secilebilir |
| Integrations — premium | - | - | Secilebilir (sinirsiz) |
| Marketing | - | - | Secilebilir |
| AgentAI — basic | - | Secilebilir | Secilebilir |
| AgentAI — premium | - | - | Secilebilir |
| Destek | Dokumantasyon | Email + Chat | Oncelikli |

**Ornek:** Profesyonel plandaki bir firma icin SuperAdmin soyle ayarlar:
```json
{
  "FlowBuilder": "premium",    // acik, premium seviye
  "Knowledge": "basic",        // acik, basic seviye
  "Outbound": "off",           // kapali (firma istemiyor / henuz lazim degil)
  "Appointments": "on",        // acik
  "Analytics": "basic",        // acik, basic seviye
  "Integrations": "basic",     // acik, 3 entegrasyon hakki
  "Marketing": "off",          // bu planda zaten yok
  "AgentAI": "on",             // acik
  "WebChat": "on"              // acik
}
```

**Kural:** SuperAdmin bir firmaya, planinin UZERINDE ozellik acamaz. Havuz disinda kalan ozellik secim listesinde gorunmez.

---

## FAZ 1: Guvenlik Temeli (Hafta 2-3)

**Amac:** Backend'i kilitlemek. Odeme almadan once, izin sistemi saglamlasmali.

### 1.1 Backend Permission Middleware (Hafta 2)

**Ne:** Her API endpoint'ine InseFeatures kontrolu ekle. Feature yoksa 403 don.

**Nasil:**
- `Invekto.Shared`'a `FeatureGuardMiddleware` ekle
- Endpoint bazli `[RequireFeature("FlowBuilder")]` attribute
- TenantContext + InseSession'dan feature kontrol
- 403 response: `{ "error": "INV-FORBIDDEN", "message": "Bu ozellik planinizda yok", "upgrade_url": "/settings/subscription" }`

**AI yuku:** Claude Code middleware + attribute kodunu yazar, testlerini olusturur.

### 1.2 Feature Tiering (Hafta 2-3)

**Ne:** `features_json` kolonunu aktif et. INMA'dan gelen features + tenant_registry override.

**Yapi:**
```json
{
  "FlowBuilder": "premium",
  "Knowledge": "basic",
  "Outbound": "off",
  "Appointments": "on",
  "Analytics": "basic",
  "Integrations": "basic",
  "Marketing": "off"
}
```

**Degerler:** `"premium"` | `"basic"` | `"on"` | `"off"`

**Frontend:** `hasFeature()` fonksiyonunu genislet → tier seviyesini de kontrol etsin. UI'da "Upgrade" badge goster.

### 1.3 Plan Kota Tablosu (Hafta 3)

**DB:**
```sql
CREATE TABLE plan_quotas (
    plan_tier    VARCHAR(20) NOT NULL,
    metric       VARCHAR(50) NOT NULL,
    quota_limit  INTEGER NOT NULL,
    PRIMARY KEY (plan_tier, metric)
);

-- Ornek veriler:
INSERT INTO plan_quotas VALUES
('basic', 'messages_per_month', 1000),
('basic', 'max_users', 5),
('basic', 'max_flows', 3),
('pro', 'messages_per_month', 10000),
('pro', 'max_users', 20),
('pro', 'max_flows', -1),  -- sinirsiz
('enterprise', 'messages_per_month', -1),
('enterprise', 'max_users', -1),
('enterprise', 'max_flows', -1);
```

**Metering:**
```sql
CREATE TABLE tenant_usage (
    tenant_id      INTEGER NOT NULL,
    metric         VARCHAR(50) NOT NULL,
    period_start   DATE NOT NULL,
    period_end     DATE NOT NULL,
    usage_count    INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (tenant_id, metric, period_start)
);
```

**AI yuku:** Claude Code SQL migration, repository kodu, ve metering middleware'i yazar.

### 1.4 super.invekto.com / crm.invekto.com Ayrimi (Hafta 3)

**Ne:** Ayni React app, farkli routing. Hosting/reverse proxy seviyesinde ayrim.

**Nasil:**
- IIS ARR'da 2 site: super.invekto.com → :5000/ops/*, crm.invekto.com → :5000/*
- React Layout.tsx: hostname'e gore opsOnly sayfalari goster/gizle
- Veya: environment variable `VITE_APP_MODE=ops|crm` ile build-time ayrim

**AI yuku:** Claude Code routing degisikligi + IIS config yazar.

---

## FAZ 2: Para Alma (Hafta 4-6)

**Amac:** QNB SanalPos entegrasyonu, abonelik yonetimi, fatura.

### 2.1 QNB SanalPos Entegrasyonu (Hafta 4-5)

**Ne:** Recurring odeme alma, kart saklama, 3D Secure.

**DB:**
```sql
CREATE TABLE tenant_subscriptions (
    id              SERIAL PRIMARY KEY,
    tenant_id       INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    plan_tier       VARCHAR(20) NOT NULL,
    status          VARCHAR(20) NOT NULL DEFAULT 'active',  -- active, past_due, cancelled, trial
    billing_period  VARCHAR(10) NOT NULL DEFAULT 'monthly', -- monthly, yearly
    amount_cents    INTEGER NOT NULL,
    currency        VARCHAR(3) NOT NULL DEFAULT 'TRY',
    current_period_start TIMESTAMPTZ NOT NULL,
    current_period_end   TIMESTAMPTZ NOT NULL,
    trial_ends_at   TIMESTAMPTZ,
    cancelled_at    TIMESTAMPTZ,
    qnb_customer_id VARCHAR(100),
    qnb_subscription_id VARCHAR(100),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE tenant_invoices (
    id              SERIAL PRIMARY KEY,
    tenant_id       INTEGER NOT NULL,
    subscription_id INTEGER REFERENCES tenant_subscriptions(id),
    invoice_number  VARCHAR(50) NOT NULL UNIQUE,
    amount_cents    INTEGER NOT NULL,
    currency        VARCHAR(3) NOT NULL DEFAULT 'TRY',
    status          VARCHAR(20) NOT NULL DEFAULT 'pending', -- pending, paid, failed, refunded
    pdf_url         VARCHAR(500),
    qnb_payment_id  VARCHAR(100),
    paid_at         TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

**Mikroservis veya Backend?** Backend'e ekle (ayri mikroservis gereksiz, basit tutmak lazim).

**Endpoint'ler:**
- `POST /api/v1/billing/subscribe` — Plan sec, kart bilgisi, 3D Secure
- `POST /api/v1/billing/change-plan` — Plan degistir (upgrade/downgrade)
- `POST /api/v1/billing/cancel` — Iptal (donem sonuna kadar aktif)
- `GET /api/v1/billing/invoices` — Fatura listesi
- `GET /api/v1/billing/invoices/:id/pdf` — Fatura PDF indir
- `POST /api/v1/billing/webhook` — QNB odeme bildirimleri
- `GET /api/v1/ops/billing/overview` — SuperAdmin: tum abonelikler

### 2.2 Billing UI - Firma Tarafi (Hafta 5-6)

**Ne:** Ayarlar > Abonelik sayfasi.

**Icerik:**
- Aktif plan ve donemi
- Sonraki fatura tarihi ve tutari
- Plan degistirme (upgrade/downgrade karsilastirma tablosu)
- Kart bilgisi guncelleme
- Fatura gecmisi (PDF indirme)
- Iptal butonu (onay dialog ile)

### 2.3 Billing UI - SuperAdmin Tarafi (Hafta 6)

**Ne:** super.invekto.com'da tenant billing yonetimi.

**Icerik:**
- Tenant bazli abonelik durumu
- Manuel plan override (ozel anlasmalar icin)
- Odeme gecmisi
- Gecmis faturalari goruntuleme
- Aktif/pasif firma toggle (odeme sorunlarinda)

### 2.4 Otomatik Islemler

- Donem sonu otomatik tahsilat (QNB recurring)
- Basarisiz odeme → 3 gun sonra tekrar dene → basarisizsa `past_due` → 7 gun sonra `suspended`
- Fatura PDF otomatik olusturma (HTML → PDF)
- Email bildirimleri: odeme alindi, odeme basarisiz, plan degisti, iptal

**AI yuku:** Claude Code tum backend kodu, DB migration, UI sayfalarini yazar. QNB API dokumanlarini okuyup entegrasyonu yapar.

---

## FAZ 3: Musteri Deneyimi (Hafta 7-8)

**Amac:** Ilk giris deneyimi, dokumantasyon tamamlama, self-service.

### 3.1 Onboarding Wizard (Hafta 7)

**Akis:**
```
INMA'da firma olusturulur
    ↓
Kullanici ilk kez crm.invekto.com'a giris yapar
    ↓
tenant_registry'de onboarding_completed = false
    ↓
┌─────────────────────────────────────────────┐
│ ONBOARDING WIZARD                           │
│                                             │
│ Adim 1: Hosgeldiniz!                        │
│   Sektor seciniz: [e-ticaret] [saglik] ...  │
│                                             │
│ Adim 2: Kanallariniz                        │
│   INMA'dan gelen kanallar listelenir        │
│   WhatsApp ✓  Instagram ✓  SMS ✗           │
│   "Yeni kanal eklemek icin INMA'ya gidin"   │
│                                             │
│ Adim 3: Ilk otomasyonunuz                   │
│   Sektorunuze ozel hazir sablon:            │
│   [Hosgeldin mesaji] [Siparis takibi] ...   │
│   Bir sablon secin → Flow otomatik olusur   │
│                                             │
│ Adim 4: Test edin                           │
│   WebChat widget'indan test mesaji gonderin │
│   Calistigini gorun ✓                       │
│                                             │
│ Adim 5: Hazirsiniz!                         │
│   → Dashboard'a git                         │
│   → Dokumantasyonu incele                   │
│   → Destek: docs.invekto.com               │
└─────────────────────────────────────────────┘
```

**DB:** `settings_json` icine `"onboarding": { "completed": true, "completed_at": "...", "sector_selected": "eticaret" }`

### 3.2 Dokumantasyon Tamamlama (Hafta 7-8, paralel)

Detaylar: `05-DOKUMANTASYON-PLANI.md`

### 3.3 WebChat Widget Kisimlestirme (Hafta 8)

**Ne:** Firma kendi WebChat widget'ini renk/logo ile kisisellestirsin.

**Nasil:** settings_json'a `"webchat": { "color": "#E54C4C", "logo_url": "...", "welcome_message": "..." }` ekle.

---

## FAZ 4: Lansman (Hafta 9-10)

**Amac:** Ilk odeme yapan musteriler.

| # | Gorev | Kim |
|---|-------|-----|
| 4.1 | invekto.com'a pricing sayfasi guncelle (gercek planlar) | AI + Q review |
| 4.2 | invekto.com'a sign-up/demo talep formu | AI yazar |
| 4.3 | Ilk 5 firma icin INMA'da hesap olustur | Q + Satis |
| 4.4 | Manuel onboarding (ilk 5 firma elden kurulur) | Q + Teknik Destek |
| 4.5 | Feedback toplama (WhatsApp grubu ile) | Satis |
| 4.6 | Hata/istek takibi (basit Trello/Notion board) | Teknik Destek |

**Hedef:** Hafta 10 sonunda en az 1 odeme yapan musteri.

---

## FAZ 5: Buyume (Hafta 11+, surekli)

| # | Gorev | Kim | AI Payi |
|---|-------|-----|---------|
| 5.1 | Blog icerigi (haftada 2 makale) | AI yazar, Satis review | %90 AI |
| 5.2 | Dokumantasyon genisletme | AI yazar, Teknik Destek review | %80 AI |
| 5.3 | SEO optimizasyonu | AI analiz + uygulama | %90 AI |
| 5.4 | Sosyal medya icerigi | AI uretir, Satis paylasar | %80 AI |
| 5.5 | WebChat'te AI asistan (musteri self-service) | AgentAI servisi | %100 AI |
| 5.6 | Otomatik sohbet cevaplama | Flow Builder + AgentAI | %100 AI |
| 5.7 | Email kampanyalari | AI yazar, Outbound servisi gonderir | %90 AI |
| 5.8 | Musteri destek (ilk seviye) | AI chatbot + Knowledge base | %70 AI |
| 5.9 | iOS app yayin (InvektoChat) | Dev | %50 AI |
| 5.10 | Invekto Softphone entegrasyonu | Dev (ileride) | - |

---

## Basari Kriterleri

| Metrik | Faz 4 Sonu (Hafta 10) | 3 Ay Sonu | 6 Ay Sonu |
|--------|----------------------|-----------|-----------|
| Odeme yapan firma | 1 | 10 | 30 |
| MRR (Aylik tekrarlayan gelir) | > 0 TL | 15.000 TL | 50.000 TL |
| Dokumantasyon kapsami | %60 | %80 | %95 |
| Uptime | - | %99 | %99.5 |
| Destek cevap suresi | 24 saat | 12 saat | 4 saat |
