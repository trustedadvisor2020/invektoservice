<!-- Status: CANCELLED | 2026-05-06 -->
# FEAT-MEDIPOL-WA — Doktor Detay WhatsApp Yönlendirme + KVKK + Slot

> **Tarih:** 2026-04-29 → **CANCELLED 2026-05-06**
> **Müşteri:** Medipol Sağlık Grubu
> **Durum:** ❌ CANCELLED — Müşteri konfirmasyonu (mail 2026-05-06): site Drupal'de, Medipol yönetiyor. Form doğrudan INMA'ya gidiyor, agent dispatch INMA'da. Doktor verisi gerekmiyor (Medipol kendi yapıyor). Ortak 1 numara. **Invekto kod tarafında yapılacak iş yok.**
> **Kategori:** DEV → CANCELLED
> **Kanban:** D028 — "Yol Haritası" board (board_key='inse'), TODO kolonu (kart kapatılacak)
> **Öncelik:** — (paket iptal)

---

## ❌ CANCEL Gerekçesi (2026-05-06)

Müşteri mail cevabı:
1. Site Drupal'de, sitedeki tüm ayarları Medipol yapacak
2. Doktor verisine ihtiyaç yok — formları ve diğer tüm bilgileri Medipol kendi yapıyor
3. Ortak 1 numara olacak
4. Form dolduran hastanın bilgileri INMA'ya gelecek, oradan ilgili agent'a düşecek

Yeni akış: `Drupal site → form → INMA → agent dispatch → WhatsApp 1 numara`. Invekto kod tarafı (KVKK middleware, doctor seed, branch tablosu, demo HTML, MedipolEndpoints, slot booking Faz 2-3) tamamen gereksiz.

Q netleştirmesi: INMA ve INSE ayrı müşteri ürünleri değil — ikisi de Invekto uygulaması. Bu vaka Drupal-INMA-only entegrasyon olduğu için Invekto backend mikroservislerine (INSE) hiçbir hop yok.

### İptal kapsamı
- ❌ Migration 043 (zaten başka pakete tahsis edildi: `043-feat-pilot-flags-vcp-phase2-kanban`)
- ❌ Backend MedipolEndpoints (4 endpoint)
- ❌ Demo HTML page
- ❌ KVKK consent log + version (INMA veya Drupal tarafında)
- ❌ Faz 2-3 slot görüntüleme/booking (Q: "o da iptal")

### Korunan
- `tracking/ideas/medipol-whatsapp-redirect.md` — historical/audit referans (CANCELLED tag eklendi)
- Bu dosya — CANCELLED rationale audit trail
- Lessons +1 entry: "Drupal-managed site + INMA agent dispatch yeterli olduğunda Invekto kod yer almaz" pattern

---

## Tarihsel İçerik (referans, paket başlatılırken yazılmıştı)

> Aşağıdaki bölümler iptal öncesi assumption-driven planlama için yazılmıştı. Müşteri yanıtı sonrası geçersiz.

---

## Karar (2026-04-29 — Q onayıyla başlangıç)

Müşteri yanıtı bekleniyordu (mail tekrar atıldı 2026-04-29). Q onayı: **assumption'larla Faz 1'i başlat, gelecek cevapla güncelle**.

### Sabit Assumption'lar (müşteri override edebilir)
| Konu | Assumption |
|------|------------|
| WhatsApp numara modeli | **Şube bazlı ortak numara** (1.500 doktor başına ayrı numara değil) — operasyonel sadelik |
| KVKK metni | **Placeholder** — müşteri hukuk biriminden gelen metni `consent_text_versions` tablosuna sonra ekleyecek |
| Mimari | **Backend altında multi-tenant feature** (yeni servis aşırı, tek müşteri için) |
| Tenant_id | Medipol = yeni tenant (INMA lazy provisioning sonrası) |
| Frontend | **Demo HTML page** (Medipol kendi site entegrasyonunda referans) — production entegrasyonu müşteri dev ekibiyle koordineli |
| Doctor/branch verisi | Q manuel seed (1-2 branch + 5-10 doctor test için) — production'da Excel/API ile gelecek |

---

## Talep Özeti (referans dökümanından)

Medipol web sitesinde doktor detay ekranlarına 2 yeni özellik:
1. **WhatsApp yönlendirme alanı** — hasta doktorla doğrudan WhatsApp üzerinden iletişime geçsin (KVKK onay akışı dahil)
2. **Randevu slotları** — doktor müsaitlik saatleri detay sayfada gösterilsin

Doktorların takvim verisi **Medipol'ün kendi CRM'inde** (entegrasyon Faz 2/3).

**Referans Model:** Doktorsitesi.com akışı (`https://www.doktorsitesi.com/doc-dr-hayrettin-temel/cocuk-sagligi-ve-hastaliklari/istanbul`).

---

## Mevcut Akış (Doktorsitesi modeli — referans)

1. Doktor detay sayfasında WhatsApp ikonu tıklanır
2. Modal açılır → Ad, Soyad, Telefon + 2 onay (zorunlu KVKK + opsiyonel pazarlama)
3. "WhatsApp'a İlerle" butonu
4. WhatsApp app/web seçim ekranı (WA'nın kendi handler'ı, biz implement etmiyoruz)
5. WhatsApp açılır, hazır mesaj görünür:
   > "Merhabalar ben {Ad Soyad}, sizlere {site} üzerinden ulaşıyorum. {Ünvan} {Doktor Adı}'dan bilgi almak istiyorum."

Numara modeli: doktora özel değil, **şube bazlı ortak numara** (örn. "Bahçelievler Ortak Numara") — operasyonel olarak 1.500 numara yerine ~10-20 şube hattı yeterli.

---

## Önerilen Mimari (Faz 1 default)

### Veri Modeli (Migration 040 — pending)

```sql
branches (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  name VARCHAR(255) NOT NULL,
  slug VARCHAR(64),                       -- URL-friendly: bahcelievler
  whatsapp_common_number VARCHAR(20),     -- E.164: +905551234567
  is_active BOOLEAN DEFAULT TRUE,
  created_at TIMESTAMPTZ DEFAULT NOW(),
  updated_at TIMESTAMPTZ DEFAULT NOW()
);
-- NOT: doctors tablosu Migration 031'de bootstrap edildi
-- Genişletilmesi gerekenler:
ALTER TABLE doctors
  ADD COLUMN IF NOT EXISTS title VARCHAR(64),               -- Doç. Dr. / Op. Dr.
  ADD COLUMN IF NOT EXISTS slug VARCHAR(128),               -- doc-dr-hayrettin-temel
  ADD COLUMN IF NOT EXISTS specialty VARCHAR(128),          -- Çocuk Sağlığı ve Hastalıkları
  ADD COLUMN IF NOT EXISTS branch_id BIGINT REFERENCES branches(id),
  ADD COLUMN IF NOT EXISTS whatsapp_number VARCHAR(20),     -- doktora özel (NULL ise branch ortak)
  ADD COLUMN IF NOT EXISTS is_whatsapp_enabled BOOLEAN DEFAULT TRUE;

whatsapp_consents (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  doctor_id BIGINT REFERENCES doctors(id),
  branch_id BIGINT REFERENCES branches(id),
  patient_name VARCHAR(255) NOT NULL,
  patient_phone VARCHAR(20) NOT NULL,    -- E.164 normalized
  consent_text_version VARCHAR(32),      -- KVKK metni versiyon takibi
  marketing_opt_in BOOLEAN DEFAULT FALSE,
  ip_address INET,
  user_agent TEXT,
  redirect_url TEXT,                     -- audit: oluşturulan wa.me URL
  created_at TIMESTAMPTZ DEFAULT NOW()
);

consent_text_versions (
  id BIGSERIAL PRIMARY KEY,
  tenant_id BIGINT NOT NULL,
  version VARCHAR(32) NOT NULL,           -- v1.0, v1.1, ...
  body_markdown TEXT NOT NULL,
  is_active BOOLEAN DEFAULT FALSE,
  effective_from TIMESTAMPTZ DEFAULT NOW(),
  UNIQUE(tenant_id, version)
);
```

### Akış

| # | Frontend | Backend |
|---|----------|---------|
| 1 | WhatsApp ikonu tıklandı (doctor detay sayfa) | — |
| 2 | KVKK modal açıldı (consent_text_versions latest active) | — |
| 3 | Form submit → POST /api/v1/medipol/consent | Insert `whatsapp_consents`, doctor.whatsapp_number ?? branch.whatsapp_common_number resolve, response `{ok, redirect_url}` |
| 4 | Tarayıcı `wa.me/{phone}?text=...`'e redirect | — |
| 5 | WhatsApp açılır, hazır mesaj | — |

### API Endpoints (Backend)

```
GET  /api/v1/medipol/doctors/{slug}              → doctor detail (public, tenant_id query param veya host-based)
GET  /api/v1/medipol/branches                    → branch list
POST /api/v1/medipol/consent                     → consent log + redirect URL
GET  /api/v1/medipol/consent/text                → latest active KVKK metni
```

### Validation & Guards

- **Phone:** TR formatı (libphonenumber-net veya regex `^\+?90?5\d{9}$`)
- **Rate limit:** Aynı IP'den 5dk'da max 3 consent (mevcut `ApiKeyRateLimiter` pattern reuse)
- **CORS:** Medipol domain'i allowlist (medipol.com.tr — müşteri konfirme edecek)
- **Bot guard:** Honeypot field (gizli input dolarsa 400) — captcha fazla aşamada eklenir

---

## Faz Yapısı

| Faz | Kapsam | Süre | Bağımlılık |
|-----|--------|------|------------|
| **Faz 1 (BAŞLIYOR)** | WhatsApp yönlendirme + KVKK onay akışı + consent loglama (1.500 doktor, şube bazlı ortak numara) | 2-3 hafta | Yok — assumption'larla |
| Faz 2 | Doktor detay ekranında **slot görüntüleme** (read-only, CRM'den) | 2-3 hafta | Müşteri CRM API doc + read access |
| Faz 3 (opsiyonel) | Web'den slot seçimi → CRM'e otomatik randevu yazma | 4-6 hafta | CRM read+write API + idempotency |

**Faz 1 bağımsız canlıya alınabilir.** Slot tarafı CRM API kalitesine bağımlı, ayrı kapsam.

---

## Müşteri Yanıtı Beklenen Sorular (mail 2026-04-25, retry 2026-04-29)

### 1. Web Sitesi Altyapısı
- Mevcut site teknolojisi (WordPress, custom .NET, vb.)?
- Frontend kod erişimi: biz mi, kendi dev ekibinizle koordineli mi?

### 2. Doktor Verisi (1.500 doktor)
- Doktor profil verileri hangi sistemde (CMS, HBYS, CRM)?
- Bize Excel/API ile mi iletilecek, canlı kaynaktan mı çekilecek?

### 3. WhatsApp Numara Stratejisi *(kritik karar)*
- Doktora ayrı numara mı, **şube bazlı ortak numara** mı? ✅ assumption: şube ortak
- Hasta WA yazdığında kim cevap verecek? (Çağrı merkezi / şube sekreteri / dış hizmet)
- Cevap süresi hedefi (SLA)?

### 4. KVKK
- Aydınlatma metni ve onay metinleri hukuk biriminizce onaylı şekilde hazır mı? ✅ assumption: placeholder

### 5. CRM & Randevu Slotları
- Doktor takviminin tutulduğu CRM hangisi?
- Dış sistemlere açık API var mı, dokümantasyon paylaşılabilir mi?
- Read-only mi, Read+Write mı?

### 6. Pilot & Zaman
- Pilot (1 şube / 50 doktor) mı, 1.500 doktor tek seferde mi?
- Hedef canlıya çıkış tarihi?

### 7. Tasarım
- Kurumsal style guide / tasarım sistemi var mı?

---

## Açık Kararlar (Q onayı bekliyor — Faz 1 plan JSON öncesi)

1. **Yeni servis vs Backend feature?** — assumption: Backend altında multi-tenant feature
2. **Demo HTML page** üretilsin mi (Medipol kendi entegrasyonu için)? — assumption: evet, statik dosya
3. **doctors/branches seed** — Q manuel mi yoksa Excel import endpoint mi? — assumption: ad-hoc SQL seed (Migration), API endpoint Faz 1 kapsamında değil
4. **Tenant_id provisioning** — Medipol için yeni tenant_id (örn. 99000xxx) önceden mi açılsın yoksa INMA lazy mi?

---

## Sonraki Adım (Q'ya sun)

1. **Plan JSON taslak** üretildi: `arch/plans/20260429-feat-medipol-wa-faz1.json` (Q onayı sonrası interview gate kapatılır)
2. Q onayı sonrası `/auto` workflow ile dev başlar:
   - Migration 040 (branches + doctors ALTER + consent + consent_text_versions)
   - Backend `MedipolEndpoints.cs` (4 endpoint)
   - Demo HTML page (`src/Invekto.Backend/wwwroot/medipol-demo/`)
   - Codex review + commit + deploy

---

## Referanslar

- Eski idea dosyası: [tracking/ideas/medipol-whatsapp-redirect.md](ideas/medipol-whatsapp-redirect.md) (PROMOTED)
- Doctors bootstrap: [arch/db/migrations/031-doctors-bootstrap.sql](../arch/db/migrations/031-doctors-bootstrap.sql)
- Plan JSON: `arch/plans/20260429-feat-medipol-wa-faz1.json` (pending)
