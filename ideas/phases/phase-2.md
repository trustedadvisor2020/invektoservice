# Phase 2 — Niche Güçlendirme

> **Hafta:** 9-16
> **MRR Hedefi:** 300-500K TL
> **Müşteri Hedefi:** 75+
> **Bağımlılık:** Phase 1 tamamlanmış olmalı
> **Durum:** ⬜ Başlamadı

---

## Durum Takibi

| Alt Gereksinim | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------------------|--------|
| GR-2.1 Intent Genişletme (e-ticaret) | ⬜ Başlamadı | — | — |
| GR-2.2 B2B / VIP Lead Tespiti | ⬜ Başlamadı | — | — |
| GR-2.3 Agent Assist Genişleme (e-ticaret) | ⬜ Başlamadı | — | — |
| GR-2.4 Hepsiburada API Entegrasyonu | ⬜ Başlamadı | — | — |
| GR-2.5 Onboarding Otomasyonu | ⬜ Başlamadı | — | — |
| GR-2.6 Kargo Entegrasyonu (opsiyonel) | ⬜ Başlamadı | — | — |
| GR-2.7 Outbound E-ticaret Senaryoları | ⬜ Başlamadı | — | — |
| GR-2.8 İade Çevirme v1 | ⬜ Başlamadı | — | — |
| GR-2.9 Diş: Intent Genişletme | ⬜ Başlamadı | — | — |
| GR-2.10 Diş: Randevu Motoru v2 | ⬜ Başlamadı | — | — |
| GR-2.11 Diş: Onboarding Otomasyonu | ⬜ Başlamadı | — | — |
| GR-2.12 Diş: Klinik Outbound v1 | ⬜ Başlamadı | — | — |
| GR-2.13 Estetik: Intent Genişletme | ⬜ Başlamadı | — | — |
| GR-2.14 Estetik: Lead Management v2 | ⬜ Başlamadı | — | — |
| GR-2.15 Estetik: Basit Ads Attribution | ⬜ Başlamadı | — | — |
| GR-2.16 Estetik: Multi-Language v1 | ⬜ Başlamadı | — | — |

---

## Özet

Sektör-özel yetenekler ekleniyor. Trendyol/HB API, randevu motoru, follow-up, intent genişleme. Niche bazlı differentiator'lar yaratılır.

**Satış dili:** "Temsilci sayınızı artırmadan 2x mesaj yönetin — sektörünüze özel otomasyon"

**Yeni Mikro Servis:**

| Servis | Port | Sorumluluk |
|--------|------|------------|
| `Invekto.Integrations` | 7106 | Trendyol/HB, randevu v2, kargo, PMS |

---

## Gereksinimler — E-ticaret

### GR-2.1: Intent Genişletme (3→10-12 intent)

> **Servis:** `ChatAnalysis` / `AgentAI`

- [ ] **2.1.1** Phase 1 feedback analizi: müşteriler en çok ne soruyor?
- [ ] **2.1.2** Yeni intent'ler ekle:
  - [ ] İade nasıl yapılır → S3 iade çevirme akışına bağla
  - [ ] Ürün değişimi
  - [ ] Fatura istiyorum
  - [ ] Sipariş iptal
  - [ ] Ürün stok durumu
  - [ ] "Toptan fiyat var mı?" / "100 adet lazım" → S5 B2B Lead
  - [ ] Negatif yorum sinyali → S1 hazırlık
- [ ] **2.1.3** Confidence threshold ayarı (düşük güven → insan)
- [ ] **2.1.4** Multi-turn conversation (takip sorusu sorabilme)

### GR-2.2: B2B / VIP Lead Tespiti

> **Servis:** `ChatAnalysis` intent + Backend

- [ ] **2.2.1** B2B sinyal algılama ("toptan", "100 adet", "kurumsal fatura")
- [ ] **2.2.2** VIP flag + otomatik etiketleme
- [ ] **2.2.3** Sales team alert (email/webhook)
- [ ] **2.2.4** Müşteri geçmişi tarama (daha önce büyük sipariş vermiş mi?)
- [ ] **2.2.5** Özel teklif akışı başlatma (template)
- [ ] **2.2.6** DB:
  ```sql
  vip_flags (id, tenant_id, customer_phone, flag_type, signal_text, sales_notified, created_at)
  ```

### GR-2.3: Agent Assist Genişleme (E-ticaret Özel)

> **Servis:** `AgentAI` + `Integrations`

- [ ] **2.3.1** Sipariş kartı (konuşma yanında müşterinin son siparişi — Trendyol/HB'den)
- [ ] **2.3.2** Basit escalation notu (devredince AI özet bırakır)
- [ ] **2.3.3** E-ticaret intent'lerine özel cevap kalitesi artırma
- [ ] **2.3.4** DB:
  ```sql
  suggested_replies (id, tenant_id, conversation_id, intent, reply_text, was_accepted, created_at)
  ```

### GR-2.4: Hepsiburada API Entegrasyonu

> **Servis:** `Invekto.Integrations` (port 7106)

- [ ] **2.4.1** Integrations servis iskeletini oluştur (port 7106)
- [ ] **2.4.2** HB API entegrasyonu (Trendyol pattern'inin kopyası)
- [ ] **2.4.3** Sipariş sync + tracking
- [ ] **2.4.4** Müşteri hangi platformdan geliyorsa oradan çek
- [ ] **2.4.5** DB:
  ```sql
  integration_accounts (id, tenant_id, provider, api_key_encrypted, status, created_at)
  orders_cache (id, tenant_id, provider, external_order_id, customer_phone, tracking_code, order_status, order_data_json, synced_at, created_at)
  ```

### GR-2.5: Onboarding Otomasyonu (5-10 müşteriye ölçek)

> **Servis:** Backend + Dashboard

- [ ] **2.5.1** Self-service Trendyol/HB API key girişi
- [ ] **2.5.2** Basit tenant setup wizard
- [ ] **2.5.3** Default intent ayarları (her müşteriye aynı başlangıç seti)
- [ ] **2.5.4** Tenant veri izolasyonu güçlendirme

### GR-2.6: Kargo Entegrasyonu (opsiyonel — müşteri istiyorsa)

> **Servis:** `Invekto.Integrations` genişleme

- [ ] **2.6.1** Aras Kargo tracking API
- [ ] **2.6.2** Yurtiçi Kargo tracking API
- [ ] **2.6.3** Kargo durumu değişince proaktif mesaj opsiyonu

### GR-2.7: Outbound E-ticaret Senaryoları

> **Servis:** `Invekto.Outbound` genişleme

- [ ] **2.7.1** Sipariş teslim edildi → "Memnun musunuz?" trigger'ı
- [ ] **2.7.2** İade talebi sonrası follow-up (T+24h)
- [ ] **2.7.3** B2B lead algılandığında sales alert (email/webhook)
- [ ] **2.7.4** Yorum geldi → otomatik mesaj prep
- [ ] **2.7.5** Tenant-bazlı trigger konfigürasyonu

### GR-2.8: İade Çevirme v1

> **Servis:** `ChatAnalysis` intent + Backend

- [ ] **2.8.1** "İade etmek istiyorum" intent'i algıla
- [ ] **2.8.2** Neden sor (kalite/beden/renk/hasarlı/fikrini değiştirdi)
- [ ] **2.8.3** Nedene göre aksiyon:
  - Beden/renk → değişim öner
  - Fikrini değiştirdi → kupon/indirim öner
  - Kalite/hasar → iade sürecini başlat
- [ ] **2.8.4** Basit conversion tracking (çevrildi/çevrilemedi)
- [ ] **2.8.5** DB:
  ```sql
  return_deflections (id, tenant_id, conversation_id, original_intent, reason_category, action_taken, was_deflected, created_at)
  ```

---

## Gereksinimler — Diş Kliniği

### GR-2.9: Diş Intent Genişletme (3→8-10 intent)

> **Servis:** `ChatAnalysis` genişleme

- [ ] **2.9.1** Phase 1 feedback: hastalar en çok ne soruyor?
- [ ] **2.9.2** Yeni intent'ler ekle:
  - [ ] Randevu değiştirme/iptal
  - [ ] Tedavi bilgisi ("İmplant ne kadar sürer?")
  - [ ] Acil durum ("Ağrım var") → doktor alert
  - [ ] Sigorta sorusu ("SGK karşılıyor mu?")
  - [ ] Adres/ulaşım → konum + yol tarifi
  - [ ] Çalışma saatleri
- [ ] **2.9.3** Confidence threshold (düşük güven → sekretere devret)

### GR-2.10: Randevu Motoru v2

> **Servis:** Backend genişleme

- [ ] **2.10.1** Google Calendar sync (2-way)
- [ ] **2.10.2** Doktor bazlı slot yönetimi (specialist vs genel)
- [ ] **2.10.3** Bekleme listesi (iptal olursa → sıradaki hastaya sor)
- [ ] **2.10.4** No-show prediction (2+ kez no-show yapan hasta → extra hatırlatma)
- [ ] **2.10.5** Randevu onay → otomatik hatırlatma zinciri (T-48h, T-2h)
- [ ] **2.10.6** DB:
  ```sql
  appointments (id, tenant_id, patient_phone, patient_name, doctor_id, service_type, slot_start, slot_end, status, reminder_sent_48h, reminder_sent_2h, confirmed, no_show, no_show_count, created_at, updated_at)
  appointment_slots (id, tenant_id, doctor_id, day_of_week, start_time, end_time, max_capacity, is_active, created_at)
  waitlist (id, tenant_id, patient_phone, preferred_date, preferred_time, service_type, status, created_at)
  service_pricing (id, tenant_id, service_name, price_min, price_max, duration_minutes, description, is_active, created_at, updated_at)
  ```

### GR-2.11: Diş Onboarding Otomasyonu (5+ klinik)

> **Servis:** Backend + Dashboard

- [ ] **2.11.1** Self-service slot tanımı
- [ ] **2.11.2** Fiyat aralığı editor (tedavi → min/max TL)
- [ ] **2.11.3** Template özelleştirme (klinik adı, doktor adı)
- [ ] **2.11.4** Tenant veri izolasyonu

### GR-2.12: Klinik Outbound v1

> **Servis:** `Invekto.Outbound` genişleme

- [ ] **2.12.1** Randevu hatırlatma otomasyonu (cron → Outbound Engine'e taşı)
- [ ] **2.12.2** Kontrol randevusu hatırlatma (tedavi sonrası T+30 gün)
- [ ] **2.12.3** Doğum günü / yıldönümü mesajı (basit template)
- [ ] **2.12.4** Opt-out yönetimi

---

## Gereksinimler — Estetik Klinik

### GR-2.13: Estetik Intent Genişletme (3→10-12 intent)

> **Servis:** `ChatAnalysis` genişleme

- [ ] **2.13.1** Yeni intent'ler ekle:
  - [ ] Before/after fotoğraf talebi
  - [ ] İşlem detayı ("Botox ne kadar sürer?")
  - [ ] Kontrendikasyon ("Hamilelikte yapılır mı?")
  - [ ] İyileşme süreci ("Ne zaman normal hayata dönebilirim?")
  - [ ] Paket sorusu ("Botox + dolgu paketi var mı?")
  - [ ] Yabancı hasta → dil algılama + İngilizce cevap
  - [ ] Referans ("Arkadaşım geldi, bana indirim var mı?")
  - [ ] Ödeme/taksit ("Taksit yapılır mı?")
- [ ] **2.13.2** Confidence threshold

### GR-2.14: Lead Management v2

> **Servis:** Backend + Dashboard

- [ ] **2.14.1** Lead source tracking (Instagram, Google, referans, organik)
- [ ] **2.14.2** Lead scoring (basit: ilgi seviyesi + bütçe + zaman)
- [ ] **2.14.3** Pipeline view (yeni → iletişim → konsültasyon → randevu → hasta)
- [ ] **2.14.4** Follow-up otomasyonu (T+24h, T+72h, T+7gün)
- [ ] **2.14.5** "Sıcak lead" alert (yüksek skor → hemen ara)
- [ ] **2.14.6** Lead → randevu → hasta dönüşüm funnel dashboard
- [ ] **2.14.7** DB:
  ```sql
  leads (id, tenant_id, phone, name, source, utm_source, utm_medium, utm_campaign, interest, score, pipeline_status, assigned_to, last_contact_at, next_followup_at, created_at, updated_at)
  lead_activities (id, lead_id, tenant_id, activity_type, note, created_at)
  service_catalog (id, tenant_id, service_name, category, price_min, price_max, duration_minutes, recovery_days, description_tr, description_en, is_active, created_at, updated_at)
  ```

### GR-2.15: Basit Ads Attribution

> **Servis:** Backend + Dashboard

- [ ] **2.15.1** UTM parameter capture (WhatsApp link'e UTM ekle)
- [ ] **2.15.2** Lead source → "Bu lead hangi kampanyadan geldi?"
- [ ] **2.15.3** Kampanya bazlı lead sayısı dashboard
- [ ] **2.15.4** Cost-per-lead hesaplama (manuel reklam maliyeti girişi)

### GR-2.16: Multi-Language v1 (basit)

> **Servis:** `ChatAnalysis` + Backend

- [ ] **2.16.1** Dil algılama (İngilizce mesaj gelince → İngilizce cevap)
- [ ] **2.16.2** İngilizce template seti (fiyat, randevu, bilgi)
- [ ] **2.16.3** Yabancı hasta flag (dashboard'da "yabancı hasta" etiketi)

---

## Çıkış Kriterleri (Phase 3'e Geçiş Şartı)

- [ ] Otomasyon kullanan toplam müşteri 30+
- [ ] Churn rate <%10 (otomasyon eklenince düşmeli)
- [ ] Müşteriler "ürün sorularına da cevap verse" diyor → Knowledge ihtiyacı
- [ ] "AI yanlış cevap veriyor" şikayeti → RAG ihtiyacı
- [ ] Outbound delivery rate %90+
- [ ] En az 1 B2B lead yakalandı

### Niche Bazlı Başarı Kriterleri

| Kriter | E-ticaret | Diş | Estetik |
|--------|-----------|-----|---------|
| Yeni müşteri | 5-10 satıcı | 3-5 klinik | 3-5 klinik |
| Niche MRR katkısı | 15-50K TL | 22-37K TL | 45-75K TL |
| Deflection rate | %40+ | N/A | N/A |
| Dönüşüm | İade çevirme %15+ | Fiyat→randevu %30+ | Lead→randevu %30+ |
| No-show | N/A | %10 altı | N/A |
| Case study | 1 yayınlanabilir | 1 yayınlanabilir | 1 yayınlanabilir |
