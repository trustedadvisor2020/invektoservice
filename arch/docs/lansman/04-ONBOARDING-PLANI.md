# Onboarding Plani

> Son guncelleme: 2 Mart 2026
> Kaynak: INMA (firma, kullanici, kanal hazir gelir)
> Prensip: INMA kurulumu tamam olan firma, crm.invekto.com'a ilk giriste yonlendirilir

---

## 1. Onboarding Akisi

### Genel Akis

```
INMA'da firma olusturulur
  │ (CompanyCode, kullanicilar, kanallar, InseFeatures tanimlanir)
  ↓
Firma admin'i INMA'dan "Invekto CRM'e git" tiklar
  │ (INMA JWT ile redirect)
  ↓
crm.invekto.com/?token=<INMA_JWT>
  │ (Token exchange → INSE JWT)
  ↓
tenant_registry'de kayit kontrol:
  ├── Kayit yok → auto-seed (mevcut davranis) + onboarding_status = "pending"
  └── Kayit var, onboarding_status = "completed" → Dashboard
  ↓
onboarding_status != "completed"
  ↓
ONBOARDING WIZARD BASLAR
```

### Wizard Adimlari

```
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  ADIM 1/5 — Hosgeldiniz!                                   │
│                                                             │
│  Invekto CRM'e hosgeldiniz, [Firma Adi]!                   │
│                                                             │
│  Sektorunuz nedir?                                          │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐      │
│  │ E-Ticaret│ │ Saglik   │ │ Emlak    │ │ Egitim   │      │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘      │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐      │
│  │ Otel     │ │ Guzellik │ │ Otomotiv │ │ Diger    │      │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘      │
│                                                             │
│                                          [Devam →]          │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  ADIM 2/5 — Kanallariniz                                   │
│                                                             │
│  INMA'dan tanimli kanallariniz:                             │
│                                                             │
│  ✅ WhatsApp Cloud API — +90 532 xxx xx xx                 │
│  ✅ Instagram — @firmaadi                                   │
│  ⬚  SMS — (henuz tanimlanmamis)                            │
│  ⬚  Telegram — (henuz tanimlanmamis)                       │
│                                                             │
│  💡 Yeni kanal eklemek icin INMA panelinize gidin.         │
│                                                             │
│                                 [← Geri]  [Devam →]        │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  ADIM 3/5 — Ilk Otomasyonunuz                              │
│                                                             │
│  Sektorunuze ozel hazir sablonlar:                          │
│                                                             │
│  ┌────────────────────────────────────────────┐             │
│  │ 🤖 Hosgeldin Mesaji                        │             │
│  │ Musteriye otomatik karsilama + menu sunma  │             │
│  │ [Bu sablonu kur]                           │             │
│  └────────────────────────────────────────────┘             │
│  ┌────────────────────────────────────────────┐             │
│  │ 📦 Siparis Takibi (e-ticaret)              │             │
│  │ "Siparis numaram ne?" sorusuna otomatik    │             │
│  │ [Bu sablonu kur]                           │             │
│  └────────────────────────────────────────────┘             │
│  ┌────────────────────────────────────────────┐             │
│  │ ⏭ Simdilik atla                            │             │
│  │ Sonra kendiniz olusturabilirsiniz          │             │
│  └────────────────────────────────────────────┘             │
│                                                             │
│                                 [← Geri]  [Devam →]        │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  ADIM 4/5 — Test Edin                                      │
│                                                             │
│  WebChat widget'inizi deneyin:                              │
│                                                             │
│  ┌──────────────────────────────────┐                       │
│  │  [Canli Onizleme]               │                       │
│  │  Merhaba! Size nasil            │                       │
│  │  yardimci olabiliriz?           │                       │
│  │                                  │                       │
│  │  [Test mesaji yazin...]          │                       │
│  └──────────────────────────────────┘                       │
│                                                             │
│  ✅ Mesaj gonderildi                                       │
│  ✅ Otomasyon tetiklendi                                   │
│  ✅ Cevap alindi                                           │
│                                                             │
│                                 [← Geri]  [Devam →]        │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  ADIM 5/5 — Hazirsiniz!                                    │
│                                                             │
│  Tebrikler! Invekto CRM'iniz kullanima hazir.              │
│                                                             │
│  Sonraki adimlar:                                           │
│  📊 Dashboard'u inceleyin                                  │
│  📖 Dokumantasyon: docs.invekto.com                        │
│  🔧 Ayarlar'dan ekibinizi davet edin                      │
│  💬 Destek: destek@invekto.com                             │
│                                                             │
│                              [Dashboard'a Git →]            │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Teknik Uygulama

### 2.1 DB Degisikligi

`settings_json` icine onboarding state ekle:

```json
{
  "onboarding": {
    "status": "completed",
    "completed_at": "2026-03-02T14:30:00Z",
    "sector_selected": "eticaret",
    "template_installed": "hosgeldin_eticaret",
    "skipped_steps": []
  }
}
```

**status degerleri:** `"pending"` | `"in_progress"` | `"completed"` | `"skipped"`

### 2.2 API Endpoint'leri

| Endpoint | Method | Aciklama |
|----------|--------|----------|
| `GET /api/v1/onboarding/status` | GET | Onboarding durumu |
| `POST /api/v1/onboarding/sector` | POST | Sektor secimi kaydet |
| `GET /api/v1/onboarding/channels` | GET | INMA'dan gelen kanallari listele |
| `GET /api/v1/onboarding/templates` | GET | Sektore ozel sablonlari getir |
| `POST /api/v1/onboarding/install-template` | POST | Secilen sablonu kur (Flow olustur) |
| `POST /api/v1/onboarding/complete` | POST | Onboarding'i tamamla |
| `POST /api/v1/onboarding/skip` | POST | Onboarding'i atla |

### 2.3 Frontend Routing

```
crm.invekto.com giris yapilir
  → Layout.tsx kontrol eder: onboarding_status == "completed" mi?
    → Evet: normal dashboard
    → Hayir: /onboarding sayfasina redirect
  → /onboarding sayfasi: 5 adimli wizard component
  → Tamamlaninca: settings_json guncelle + /dashboard'a redirect
```

### 2.4 Sektor Sablonlari

| Sektor | Sablon Adi | Flow Icerigi |
|--------|-----------|-------------|
| E-Ticaret | hosgeldin_eticaret | Karsilama → urun sorgulama → siparis takibi → operatore aktar |
| Saglik | hosgeldin_saglik | Karsilama → randevu talebi → doktor bilgisi → operatore aktar |
| Emlak | hosgeldin_emlak | Karsilama → ilan sorgulama → fiyat bilgisi → operatore aktar |
| Egitim | hosgeldin_egitim | Karsilama → kurs bilgisi → kayit formu → operatore aktar |
| Otel | hosgeldin_otel | Karsilama → oda musaitligi → fiyat → rezervasyon → operatore aktar |
| Guzellik | hosgeldin_guzellik | Karsilama → hizmetler → randevu → operatore aktar |
| Genel | hosgeldin_genel | Karsilama → bilgi talebi → operatore aktar |

**Sablonlar:** InvektoServices'ta `Automation` mikroservisinde JSON flow tanimla olarak saklanir. Onboarding sirasinda secilen sablon tenant'a kopyalanir.

---

## 3. AI Katki Ozeti

| Gorev | AI Payi |
|-------|---------|
| Wizard UI kodlama | %90 (Claude Code React component yazar) |
| Backend endpoint'leri | %90 |
| Sektor sablonlari (Flow JSON) | %80 (AI olusturur, Q duzenler) |
| Test mesaj akisi | %85 |
| Gorsel tasarim | %70 (AI + mevcut INSE style guide) |

---

## 4. Ilk 10 Firma icin Gecis Plani

Ilk 10 firma icin wizard otomatik ama Q/Teknik Destek destekle:

| Adim | Otomatik | Manuel Destek |
|------|----------|--------------|
| INMA'da firma olusturma | - | Satis ekibi yapar |
| Token exchange + ilk giris | Otomatik | - |
| Sektor secimi | Wizard | Gerekirse rehberlik |
| Kanal kontrol | Wizard | INMA'da kanal kurulumu destekle |
| Sablon kurulumu | Wizard | Ilk 10 firma icin telefon/WhatsApp ile destek |
| Test | Wizard | Birlikte test yap |
| Go-live | Wizard tamamlanir | "Bir sorun olursa arayabilirsiniz" |

**Hedef:** Wizard yeterince iyi olunca (10+ firma sonrasi) manuel destek azalir → tamamen self-service.
