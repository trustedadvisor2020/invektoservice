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
>
> **v6 (2026-02-16):** 4 yeni GR eklendi: GR-3.26 Opt-in (CS-01), GR-3.27 SLA (CS-04),
> GR-3.28 Proaktif Güncelleme (EB-03), GR-3.29 Compliance Temel (CS-08). Toplam: 10 GR.

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
| GR-3.26 Opt-in Toplama Framework | ⬜ Başlamadı | — | v6: CS-01, tüm outbound prerequisite |
| GR-3.27 SLA Watchdog / Failover | ⬜ Başlamadı | — | v6: CS-04, operasyonel güvenilirlik |
| GR-3.28 Proaktif Sipariş Güncelleme | ⬜ Başlamadı | — | v6: EB-03, kriz öncesi bilgilendirme |
| GR-3.29 Compliance Temel (KVKK/GDPR) | ⬜ Başlamadı | — | v6: CS-08 kısmen, tam → Phase 4 GR-4.9 |

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

### GR-3.26: Opt-in Toplama Framework

> **Servis:** `Invekto.Outbound` + Backend
> **Kaynak:** CS-01 (B1.1 — Opt-in Toplama Senaryosu)
> **Etki:** BLOCKER — bu olmadan tüm outbound senaryoları yasal olarak çalışmaz
>
> **v6 (2026-02-16):** Cross-sektör kritik eksiklik. 5 raporun 4'ü tespit etti.

- [ ] **3.26.1** Opt-in toplama kanalları: ilk WA mesajında, web formunda, sipariş onayında, randevu formunda
- [ ] **3.26.2** Opt-in saklama: müşteri profilinde `wa_opt_in`, `opt_in_date`, `opt_in_source`
- [ ] **3.26.3** Opt-out yönetimi: "STOP" mesajı → otomatik unsubscribe + onay mesajı
- [ ] **3.26.4** Kategori bazlı onam: utility vs marketing template ayrımı
- [ ] **3.26.5** Compliance log: kim, ne zaman, hangi kanaldan opt-in verdi
- [ ] **3.26.6** DB:
  ```sql
  consent_records (id, tenant_id, customer_phone, consent_type, channel, source, opted_in, opted_in_at, opted_out_at, created_at)
  ```

---

### GR-3.27: SLA Watchdog / Failover

> **Servis:** Backend + `Invekto.Outbound`
> **Kaynak:** CS-04 (B1.4 — SLA Watchdog)
> **Etki:** YÜKSEK — operasyonel güvenilirlik
>
> **v6 (2026-02-16):** Cross-sektör. Mesaj bekleme süresi aşıldığında otomatik müdahale.

- [ ] **3.27.1** SLA kuralları (tenant bazlı): genel 5dk, VIP 2dk, acil sağlık 1dk
- [ ] **3.27.2** Watchdog: SLA-1dk → agent'e push, SLA doldu → supervisor eskalasyon
- [ ] **3.27.3** Emergency routing: SLA 2x aşıldı → müsait agent veya AI fallback mesajı
- [ ] **3.27.4** AI failover: servis down → "Şu an yoğunuz, en kısa sürede döneceğiz" template
- [ ] **3.27.5** Dashboard: SLA breach sayısı, ortalama bekleme, recovery süresi
- [ ] **3.27.6** DB:
  ```sql
  sla_rules (id, tenant_id, priority, max_response_sec, escalation_target, created_at, updated_at)
  sla_breaches (id, tenant_id, conversation_id, rule_id, breach_type, resolved_at, created_at)
  ```

---

### GR-3.28: Proaktif Sipariş Durum Güncelleme

> **Servis:** `Invekto.Outbound` + `Invekto.Integrations`
> **Kaynak:** EB-03 (B2.3 — Proaktif Güncelleme)
> **Sektör:** E-ticaret
> **Etki:** YÜKSEK — şikayet önleme
>
> **v6 (2026-02-16):** Kargo gecikmesi/stok sorunu → müşteriden ÖNCE bilgilendir.

- [ ] **3.28.1** Gecikme tespiti: Integrations'tan sipariş durumu değişikliği izleme
- [ ] **3.28.2** Proaktif bildirim: "Siparişinizdeki X ürünü 2 gün gecikmeli gönderilecek"
- [ ] **3.28.3** Stok sorunu bilgilendirme: stok bitince alternatif veya bekleme süresi
- [ ] **3.28.4** Template yapılandırma: tenant bazlı mesaj şablonları

---

### GR-3.29: Compliance Temel (KVKK/GDPR Framework)

> **Servis:** Backend + `Invekto.Outbound`
> **Kaynak:** CS-08 (B1.8 — Compliance Otomasyonu) — TEMEL KATMAN
> **Tam enterprise compliance:** Phase 4 GR-4.9
> **Etki:** YÜKSEK — yasal zorunluluk
>
> **v6 (2026-02-16):** Cross-sektör. Temel consent + saklama + silme altyapısı.

- [ ] **3.29.1** Explicit consent flow: her kanalda açık onam toplama (GR-3.26 ile entegre)
- [ ] **3.29.2** Template audit trail: gönderilen her template mesajın kaydı
- [ ] **3.29.3** Veri silme hakkı: müşteri "verimi silin" → temel iş akışı
- [ ] **3.29.4** Saklama süresi: sağlık X yıl, ticari Y yıl (tenant bazlı config)
- [ ] **3.29.5** Temel maskeleme: TC kimlik, telefon numarası görüntülemede maskeleme

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

- v4.3'te Phase 3'ten bölündü (6/22 GR — platform enablers), v6'da 10 GR'a çıktı (+4 CS/EB)
- Integrations servisi (:7106) bu phase'te doğar
- 3B'deki niche GR'lar bu altyapıya bağımlı: sipariş kartı (3.3→3.4), yorum kurtarma (3.16→3.4), iade v2 (3.17→3.8)
- Outbound v2 tüm niche outbound senaryolarının (3.7, 3.11, 3.20, 3.21) temeli
