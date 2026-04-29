<!-- Status: PROMOTED → tracking/feat-medipol-wa.md (2026-04-29) -->
# Medipol — Doktor Detay WhatsApp Yönlendirme + KVKK + Slot Görüntüleme

> **⚠️ TAŞINDI:** Bu fikir 2026-04-29'da [tracking/feat-medipol-wa.md](../feat-medipol-wa.md)'ye promote edildi. Aktif geliştirme oraya taşındı (Faz 1 IN_PROGRESS, assumption-driven). Bu dosya audit/historical referans için korunuyor.

---

> **Tarih:** 2026-04-25
> **Müşteri:** Medipol Sağlık Grubu
> **Durum:** WAITING_CUSTOMER_RESPONSE — sorular maille iletildi, scope netleşmedi
> **Sektör:** Sağlık (B2C, hastane grubu, 1.500 doktor)
> **Referans Model:** Doktorsitesi.com akışı

---

## Talep Özeti

Medipol'ün web sitesinde doktor detay ekranlarına iki yeni özellik:

1. **WhatsApp yönlendirme alanı** — hasta doktorla doğrudan WhatsApp üzerinden iletişime geçsin (KVKK onay akışı dahil)
2. **Randevu slotları** — doktor müsaitlik saatleri detay sayfada gösterilsin

Doktorların takvim verisi **Medipol'ün kendi CRM'inde** tutuluyor (entegrasyon gerekecek).

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

## Önerilen Mimari

### Veri Modeli

```sql
doctors (
  doctor_id, name, title, branch_id,
  whatsapp_number,        -- doktora özel ya da NULL (şube ortak numarası)
  is_whatsapp_enabled
)

branches (
  branch_id, name,
  whatsapp_common_number  -- "Bahçelievler Ortak Numara" gibi
)

whatsapp_consents (
  id, doctor_id,
  patient_name, patient_phone,
  consent_text_version,   -- KVKK metni versiyon takibi (denetim için)
  marketing_opt_in,       -- ikinci checkbox (opsiyonel)
  ip_address, user_agent,
  created_at
)
```

### Akış

| # | Frontend | Backend |
|---|----------|---------|
| 1 | WhatsApp ikonu tıklandı | — |
| 2 | KVKK modal açıldı | — |
| 3 | Submit → POST /consent | Insert `whatsapp_consents`, response `{ok, redirect_url}` |
| 4 | Tarayıcı `wa.me/{phone}?text=...`'e yönlendirir | — |
| 5 | WhatsApp açılır, hazır mesaj | — |

### Teknik Notlar

- **WhatsApp link:** `https://wa.me/<E164>?text=<URL_encoded>` — app/web seçimi WA'ya bırakılır
- **KVKK consent log ZORUNLU** — denetim/itiraz durumu için `consent_text_version` kritik
- **Rate limit:** Aynı IP'den 5 dk'da max 3 consent (spam koruması)
- **Telefon validasyonu:** TR formatı (libphonenumber-js veya `^\+?90?5\d{9}$`)

---

## Faz Yapısı (Müşteriye Önerilen)

| Faz | Kapsam | Süre |
|-----|--------|------|
| **Faz 1** | WhatsApp yönlendirme + KVKK onay akışı + consent loglama (1.500 doktor, şube bazlı ortak numara) | 2-3 hafta |
| **Faz 2** | Doktor detay ekranında **slot görüntüleme** (read-only, CRM'den) | 2-3 hafta |
| **Faz 3 (opsiyonel)** | Web'den slot seçimi → CRM'e otomatik randevu yazma | 4-6 hafta |

**Faz 1 bağımsız canlıya alınabilir.** Slot tarafı CRM API kalitesine bağımlı, ayrı kapsam.

### Senaryo Karşılaştırması — Slot Tarafı

**A) Read-only (önerimiz):** Slotlar görünür, hasta seçmez, WA mesajına tarih/saat tercihi otomatik eklenir, randevu çağrı merkezi tarafından CRM'de manuel açılır.
- Avantaj: 2-3 hafta, çift yönlü senkron riski yok
- Dezavantaj: Çağrı merkezi yükü kalır

**B) Read+Write:** Hasta web'den slot seçer, biz CRM'e yazarız, doktor takviminde slot kapanır.
- Avantaj: Çağrı merkezi yükü düşer
- Dezavantaj: 6-8 hafta, idempotency + retry + double-booking guard, CRM API kalitesine bağımlı

---

## Müşteriye Sorulan Gri Noktalar (mail iletildi 2026-04-25)

### 1. Web Sitesi Altyapısı
- Mevcut site teknolojisi (WordPress, custom .NET, vb.)?
- Frontend kod erişimi: biz mi, kendi dev ekibinizle koordineli mi?

### 2. Doktor Verisi (1.500 doktor)
- Doktor profil verileri hangi sistemde (CMS, HBYS, CRM)?
- Bize Excel/API ile mi iletilecek, canlı kaynaktan mı çekilecek?

### 3. WhatsApp Numara Stratejisi *(en kritik karar)*
- Doktora ayrı numara mı, şube bazlı ortak numara mı?
- Hasta WA yazdığında kim cevap verecek? (Çağrı merkezi / şube sekreteri / dış hizmet)
- Cevap süresi hedefi (SLA)?

### 4. KVKK
- Aydınlatma metni ve onay metinleri hukuk biriminizce onaylı şekilde hazır mı?

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

## Kararlar / Asumptionlar (müşteri onayı bekliyor)

- ✋ Mimari: Faz 1 standalone, Faz 2-3 CRM API'ye bağımlı
- ✋ Numara modeli: şube bazlı ortak numara öneriliyor (doktor başına ayrı yerine)
- ✋ Slot Faz 2: read-only öneriliyor
- ✋ KVKK consent log retention: 2 yıl (kurum politikası teyit bekliyor)

---

## Sonraki Adım

1. Müşteri yanıtı bekleniyor (yukarıdaki 7 başlık)
2. Yanıt geldikten sonra:
   - Bu dosya `tracking/feat-medipol-wa.md` olarak taşınır
   - Faz 1 için plan JSON: `arch/plans/YYYYMMDD-feat-medipol-wa-faz1.json`
   - `tracking/README.md` master tabloya eklenir
3. Pilot Mode dışı bir müşteri işi — `pilot-launch-roadmap.md` queue'sunu etkilemez
