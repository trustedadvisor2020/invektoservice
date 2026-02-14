# Phase 3A — Platform Enablers

> **Hafta:** 17-20
> **MRR Hedefi:** 500-650K TL
> **Müşteri Hedefi:** 85+
> **Bağımlılık:** Phase 2 tamamlanmış olmalı
> **Durum:** ⬜ Başlamadı
>
> **v4.3 Bölünme (2026-02-14):** Phase 3 (22 GR) ikiye bölündü. 3A platform altyapısını kurar
> (Integrations servisi, Outbound v2, Randevu Advanced, Dashboard genişletme, Ads Attribution).
> 3B'deki niche GR'lar bu altyapıya bağımlıdır — 3A önce tamamlanmalı.

---

## Durum Takibi

| Alt Gereksinim | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------------------|--------|
| GR-3.4 Hepsiburada API Entegrasyonu | ⬜ Başlamadı | — | Integrations servisi burada doğar |
| GR-3.6 Kargo Entegrasyonu (opsiyonel) | ⬜ Başlamadı | — | Integrations genişleme |
| GR-3.14 Ads Attribution (Basit + Full) | ⬜ Başlamadı | — | v4.2: GR-5.6 birleştirildi |
| GR-3.15 Outbound Engine v2 | ⬜ Başlamadı | — | Kampanya + A/B + conversion |
| GR-3.18 Dashboard Genişletme | ⬜ Başlamadı | — | Outbound + iade + yorum panelleri |
| GR-3.19 Randevu Motoru v2 (Advanced) | ⬜ Başlamadı | — | v4.2: Phase 2'den bölünen advanced |

---

## Özet

Platform altyapısını kuran GR'lar. Integrations servisini (:7106) doğurur, Outbound Engine'i v2'ye yükseltir, randevu motorunu geliştirir, dashboard'u genişletir. 3B'deki tüm niche GR'lar bu altyapıyı kullanır.

**Neden ayrı phase?**
- Integrations servisi 3B'deki sipariş kartı (GR-3.3), yorum kurtarma (GR-3.16), iade v2 (GR-3.17) için şart
- Outbound v2 tüm niche outbound senaryolarının temeli
- Dashboard genişletme tüm niche metriklerini gösterecek
- Randevu Advanced sağlık genişleme GR'larının temeli

**Yeni Mikro Servis:**

| Servis | Port | Sorumluluk |
|--------|------|------------|
| `Invekto.Integrations` | 7106 | Trendyol/HB, kargo, PMS entegrasyonları |

---

## Gereksinimler

### GR-3.4: Hepsiburada API Entegrasyonu

> **Servis:** `Invekto.Integrations` (port 7106) — YENİ
> **Kaynak:** eski GR-2.4

- [ ] **3.4.1** Integrations servis iskeletini oluştur (port 7106)
- [ ] **3.4.2** HB API entegrasyonu (Trendyol pattern'inin kopyası)
- [ ] **3.4.3** Sipariş sync + tracking
- [ ] **3.4.4** Müşteri hangi platformdan geliyorsa oradan çek
- [ ] **3.4.5** DB:
  ```sql
  integration_accounts (id, tenant_id, provider, api_key_encrypted, status, created_at)
  orders_cache (id, tenant_id, provider, external_order_id, customer_phone, tracking_code, order_status, order_data_json, synced_at, created_at)
  ```

---

### GR-3.6: Kargo Entegrasyonu (opsiyonel)

> **Servis:** `Invekto.Integrations` genişleme
> **Kaynak:** eski GR-2.6

- [ ] **3.6.1** Aras Kargo tracking API
- [ ] **3.6.2** Yurtiçi Kargo tracking API
- [ ] **3.6.3** Kargo durumu değişince proaktif mesaj opsiyonu

---

### GR-3.14: Ads Attribution (Basit + Full)

> **Servis:** Backend + Dashboard
> **Kaynak:** eski GR-2.15 + **eski GR-5.6 birleştirildi (v4.2)**
>
> **v4.2:** Phase 5'teki Click-to-WhatsApp Attribution (GR-5.6) buraya taşındı.
> UTM + Meta click id + full attribution tek GR'da çözülür.

- [ ] **3.14.1** UTM parameter capture (WhatsApp link'e UTM ekle)
- [ ] **3.14.2** Lead source → "Bu lead hangi kampanyadan geldi?"
- [ ] **3.14.3** Kampanya bazlı lead sayısı dashboard
- [ ] **3.14.4** Cost-per-lead hesaplama (manuel reklam maliyeti girişi)
- [ ] **3.14.5** Meta click id capture (lead source = campaign/adset/ad) — ← eski GR-5.6.1
- [ ] **3.14.6** Pipeline auto-tagging (label + segment + UTM mapping) — ← eski GR-5.6.2
- [ ] **3.14.7** Full attribution dashboard (kampanya → lead → conversion) — ← eski GR-5.6.3

---

### GR-3.15: Outbound Engine v2

> **Servis:** `Invekto.Outbound` genişleme
> **Sektör:** Tümü
> **Kaynak:** eski GR-3.4

- [ ] **3.15.1** Campaign yönetimi (kampanya oluştur, hedef kitle seç, zamanlama)
- [ ] **3.15.2** AI-generated personalization (müşteri geçmişine göre mesaj, Knowledge ile)
- [ ] **3.15.3** Conversion tracking (mesaj → aksiyon: cevap/satın alma/randevu)
- [ ] **3.15.4** A/B testing (2 şablon → hangisi daha iyi dönüyor)
- [ ] **3.15.5** Time-based trigger'lar (T+Xh delay, recurring schedule)
- [ ] **3.15.6** ROI dashboard (kampanya bazlı gelir etkisi)
- [ ] **3.15.7** DB:
  ```sql
  outbound_campaigns (id, tenant_id, name, trigger_type, target_criteria_json, template_id, schedule_json, status, stats_json, created_at, updated_at)
  outbound_conversions (id, tenant_id, message_id, campaign_id, conversion_type, value_amount, created_at)
  ```

---

### GR-3.18: Dashboard Genişletme

> **Servis:** Dashboard
> **Sektör:** Tümü
> **Kaynak:** eski GR-3.8

- [ ] **3.18.1** Outbound campaign dashboard (gönderim/okunma/dönüşüm)
- [ ] **3.18.2** İade çevirme oranı + kurtarılan gelir
- [ ] **3.18.3** Yorum kurtarma oranı + etki
- [ ] **3.18.4** Niche bazlı dashboard panelleri (e-ticaret / diş / estetik)

---

### GR-3.19: Randevu Motoru v2 (Advanced)

> **Servis:** Backend genişleme + `Invekto.Outbound`
> **Sektör:** Sağlık (Diş + Estetik)
> **Kaynak:** Phase 2 GR-2.4'ten bölünen advanced items (v4.2)
> **Bağımlılık:** GR-2.4 (Randevu Core) tamamlanmış olmalı

- [ ] **3.19.1** Google Calendar sync (2-way)
- [ ] **3.19.2** Doktor bazlı slot yönetimi (specialist vs genel)
- [ ] **3.19.3** Bekleme listesi (iptal olursa → sıradaki hastaya sor)
- [ ] **3.19.4** No-show prediction (2+ kez no-show yapan hasta → extra hatırlatma)
- [ ] **3.19.5** Fiyat aralığı editor (tedavi → min/max TL)
- [ ] **3.19.6** DB:
  ```sql
  waitlist (id, tenant_id, patient_phone, preferred_date, preferred_time, service_type, status, created_at)
  service_pricing (id, tenant_id, service_name, price_min, price_max, duration_minutes, description, is_active, created_at, updated_at)
  ```

---

## Çıkış Kriterleri (Phase 3B'ye Geçiş Şartı)

- [ ] Integrations servisi (:7106) çalışıyor, HB API bağlı
- [ ] Outbound v2 kampanya oluşturma + A/B testing çalışıyor
- [ ] Ads Attribution UTM capture + dashboard aktif
- [ ] Dashboard'da outbound + attribution panelleri görünüyor
- [ ] Randevu Advanced: Google Calendar sync + bekleme listesi çalışıyor
- [ ] En az 2 tenant Integrations kullanıyor

---

## Notlar

- v4.3'te Phase 3'ten bölündü (6/22 GR — platform enablers)
- Integrations servisi (:7106) bu phase'te doğar
- 3B'deki niche GR'lar bu altyapıya bağımlı: sipariş kartı (3.3→3.4), yorum kurtarma (3.16→3.4), iade v2 (3.17→3.8)
- Outbound v2 tüm niche outbound senaryolarının (3.7, 3.11, 3.20, 3.21) temeli
