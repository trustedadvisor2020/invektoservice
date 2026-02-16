# Phase 3B — Niche Derinleştirme

> **Hafta:** 21-24
> **MRR Hedefi:** 650-800K TL
> **Müşteri Hedefi:** 100+
> **Bağımlılık:** Phase 3A tamamlanmış olmalı
> **Durum:** ⬜ Başlamadı
>
> **v4.3 Bölünme (2026-02-14):** Phase 3 (22 GR) ikiye bölündü. 3B sektöre özel intent'ler,
> lead pipeline, outbound senaryoları ve sağlık genişleme GR'larını içerir.
> 3A'daki platform altyapısını (Integrations, Outbound v2, Dashboard) kullanır.
>
> **v4.5 (2026-02-14):** 3 yeni GR eklendi: GR-3.23 Voice Message AI (evrensel),
> GR-3.24 Proactive Review Rescue (e-ticaret), GR-3.25 Multilingual Medical Tourism (sağlık).
>
> **v5.1 Bölünme (2026-02-15):** 19 GR, 3 alt pakete bölündü (Codex PASS olasılığı artırmak için):
> - **PKT-6A Foundation** (7 GR): GR-3.1, 3.2, 3.5, 3.9, 3.10, 3.12, 3.23
> - **PKT-6B Business Logic** (7 GR): GR-3.7, 3.8, 3.11, 3.13, 3.3, 3.16, 3.17
> - **PKT-6C Health Expansion** (5 GR): GR-3.20, 3.21, 3.22, 3.24, 3.25
> Bağımlılık: PKT-5 → 6A (bağımsız) | PKT-5 → 6B (Integrations) | 6B → 6C
>
> **v6 (2026-02-16):** 15 yeni GR eklendi (CS/EB/SB boşlukları):
> - **Cross-Sektör:** GR-3.30 Handoff, 3.31 Guardrail, 3.32 Churn, 3.33 Timeline, 3.34 Attribution
> - **E-ticaret Boşluk:** GR-3.35~3.40 (Stok, Influencer, Cross-Platform, Şikayetvar, Garanti, Fraud)
> - **Sağlık Boşluk:** GR-3.41~3.44 (Tedavi Onay, Çoklu Şube, Pre-op, Reçete)
> PKT-6A'ya +4, PKT-6B'ye +7, PKT-6C'ye +4. Toplam: 34 GR.

---

## Durum Takibi

| Alt Gereksinim | Paket | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------|-------------------|--------|
| **E-TİCARET** | | | | |
| GR-3.1 Intent Genişletme + Oto. Etiketleme | **PKT-6A** | ⬜ Başlamadı | — | ← eski GR-2.1 |
| GR-3.2 B2B / VIP Lead Tespiti | **PKT-6A** | ⬜ Başlamadı | — | ← eski GR-2.2 |
| GR-3.3 Agent Assist Genişleme (E-ticaret) | **PKT-6B** | ⬜ Başlamadı | — | ← PKT-5 Integrations gerekli |
| GR-3.5 Onboarding Otomasyonu | **PKT-6A** | ⬜ Başlamadı | — | ← eski GR-2.5 |
| GR-3.7 Outbound E-ticaret Senaryoları | **PKT-6B** | ⬜ Başlamadı | — | ← eski GR-2.7 |
| GR-3.8 İade Çevirme v1 | **PKT-6B** | ⬜ Başlamadı | — | ← eski GR-2.8 |
| **DİŞ KLİNİĞİ** | | | | |
| GR-3.9 Diş Intent + Fiyat Pipeline | **PKT-6A** | ⬜ Başlamadı | — | ← eski GR-2.9 |
| GR-3.10 Diş Onboarding Otomasyonu | **PKT-6A** | ⬜ Başlamadı | — | ← eski GR-2.11 |
| GR-3.11 Klinik Outbound v1 | **PKT-6B** | ⬜ Başlamadı | — | ← eski GR-2.12 |
| **ESTETİK KLİNİK** | | | | |
| GR-3.12 Estetik Intent + Lead Pipeline | **PKT-6A** | ⬜ Başlamadı | — | ← eski GR-2.13 |
| GR-3.13 Lead Management v2 | **PKT-6B** | ⬜ Başlamadı | — | ← eski GR-2.14 |
| **PLATFORM (BAĞIMLI)** | | | | |
| GR-3.16 Negatif Yorum Kurtarma | **PKT-6B** | ⬜ Başlamadı | — | ← PKT-5 Integrations gerekli |
| GR-3.17 İade Çevirme v2 | **PKT-6B** | ⬜ Başlamadı | — | ← GR-3.8 (aynı paket) |
| GR-3.24 Proactive Review Rescue | **PKT-6C** | ⬜ Başlamadı | — | ← GR-3.8+3.16 (PKT-6B) gerekli |
| **SAĞLIK GENİŞLEME** | | | | |
| GR-3.20 Tedavi Sonrası Takip | **PKT-6C** | ⬜ Başlamadı | — | ← eski GR-5.7 (v4.2) |
| GR-3.21 Google Yorum + Referans Motoru | **PKT-6C** | ⬜ Başlamadı | — | ← eski GR-5.8 (v4.2) |
| GR-3.22 Medikal Turizm Lead (AR hariç) | **PKT-6C** | ⬜ Başlamadı | — | ← eski GR-5.9 (v4.2, AR → Phase 5) |
| GR-3.25 Multilingual Medical Tourism | **PKT-6C** | ⬜ Başlamadı | — | ← GR-3.22 (aynı paket) |
| **EVRENSEL AI** | | | | |
| GR-3.23 Voice Message AI | **PKT-6A** | ⬜ Başlamadı | — | Whisper transkript + mevcut AgentAI pipeline |
| **CROSS-SEKTÖR (v6)** | | | | |
| GR-3.30 AI→İnsan Handoff | **PKT-6A** | ⬜ Başlamadı | — | v6: CS-02, eskalasyon kuralları |
| GR-3.31 AI Hallucination Guardrail | **PKT-6A** | ⬜ Başlamadı | — | v6: CS-03, konu bazlı guardrail |
| GR-3.32 Churn Sinyali Tespiti | **PKT-6B** | ⬜ Başlamadı | — | v6: CS-05, sentiment bazlı kayıp riski |
| GR-3.33 Unified Customer Timeline | **PKT-6B** | ⬜ Başlamadı | — | v6: CS-06, çok kanallı müşteri geçmişi |
| GR-3.34 Revenue Attribution | **PKT-6B** | ⬜ Başlamadı | — | v6: CS-07, kanal→satış takibi |
| **E-TİCARET BOŞLUK (v6)** | | | | |
| GR-3.35 Stok Bildirim (Back-in-Stock) | **PKT-6B** | ⬜ Başlamadı | — | v6: EB-01, stok girişi→WA mesaj |
| GR-3.36 Influencer/Affiliate Attribution | **PKT-6B** | ⬜ Başlamadı | — | v6: EB-02, kampanya bazlı etiketleme |
| GR-3.37 Cross-Platform Sipariş Eşleştirme | **PKT-6B** | ⬜ Başlamadı | — | v6: EB-04, telefon→sipariş birleştirme |
| GR-3.38 Şikayetvar Eskalasyon | **PKT-6B** | ⬜ Başlamadı | — | v6: EB-05, proaktif şikayet çözümü |
| GR-3.39 Garanti ve Teknik Servis | **PKT-6A** | ⬜ Başlamadı | — | v6: EB-06, Knowledge bazlı garanti akışı |
| GR-3.40 Fraud / Dolandırıcılık Şüphesi | **PKT-6A** | ⬜ Başlamadı | — | v6: EB-07, acil güvenlik eskalasyonu |
| **SAĞLIK BOŞLUK (v6)** | | | | |
| GR-3.41 Tedavi Planı Onay Akışı | **PKT-6C** | ⬜ Başlamadı | — | v6: SB-01, plan→onay follow-up zinciri |
| GR-3.42 Çoklu Klinik/Şube Yönetimi | **PKT-6C** | ⬜ Başlamadı | — | v6: SB-03, konum bazlı routing |
| GR-3.43 Tedavi Öncesi Hazırlık Talimatları | **PKT-6C** | ⬜ Başlamadı | — | v6: SB-04, pre-op mesaj zinciri |
| GR-3.44 Reçete/İlaç Sorguları | **PKT-6C** | ⬜ Başlamadı | — | v6: SB-05, Knowledge + guardrail |

---

## Özet

Sektör-özel yetenekler ekleniyor. Phase 3A'daki platform altyapısı (Integrations, Outbound v2, Randevu Advanced) sayesinde niche-özel intent'ler, lead pipeline, outbound senaryoları ve sağlık genişleme GR'ları çalışabilir.

**Satış dili:** "Temsilci sayınızı artırmadan 2x mesaj yönetin — sektörünüze özel otomasyon"

**3A bağımlılıkları:**
- GR-3.3 (Agent Assist E-ticaret) → Integrations'tan sipariş kartı çeker
- GR-3.16 (Yorum Kurtarma) → Integrations'tan yorum çeker (GR-3.4)
- GR-3.17 (İade v2) → Integrations'tan stok sorgular
- GR-3.7, 3.11, 3.20, 3.21 → Outbound v2 kampanya altyapısını kullanır

---

## Gereksinimler — E-ticaret

### GR-3.1: Intent Genişletme + Otomatik Etiketleme (3→10-12 intent)

> **Servis:** `ChatAnalysis` / `AgentAI`
> **Kaynak:** eski GR-2.1 + GR-1.2.4

- [ ] **3.1.0** Otomatik etiketleme: AI bazlı konu tespiti → etiket ata
- [ ] **3.1.1** Phase 1-2 feedback analizi: müşteriler en çok ne soruyor?
- [ ] **3.1.2** Yeni intent'ler ekle:
  - [ ] İade nasıl yapılır → iade çevirme akışına bağla
  - [ ] Ürün değişimi
  - [ ] Fatura istiyorum
  - [ ] Sipariş iptal
  - [ ] Ürün stok durumu
  - [ ] "Toptan fiyat var mı?" / "100 adet lazım" → B2B Lead
  - [ ] Negatif yorum sinyali
- [ ] **3.1.3** Confidence threshold ayarı (düşük güven → insan)
- [ ] **3.1.4** Multi-turn conversation (takip sorusu sorabilme)

### GR-3.2: B2B / VIP Lead Tespiti

> **Servis:** `ChatAnalysis` intent + Backend
> **Kaynak:** eski GR-2.2

- [ ] **3.2.1** B2B sinyal algılama ("toptan", "100 adet", "kurumsal fatura")
- [ ] **3.2.2** VIP flag + otomatik etiketleme
- [ ] **3.2.3** Sales team alert (email/webhook)
- [ ] **3.2.4** Müşteri geçmişi tarama (daha önce büyük sipariş vermiş mi?)
- [ ] **3.2.5** Özel teklif akışı başlatma (template)
- [ ] **3.2.6** DB:
  ```sql
  vip_flags (id, tenant_id, customer_phone, flag_type, signal_text, sales_notified, created_at)
  ```

### GR-3.3: Agent Assist Genişleme (E-ticaret Özel)

> **Servis:** `AgentAI` + `Integrations`
> **Kaynak:** eski GR-2.3
> **Bağımlılık:** GR-3.4 (Integrations servisi — Phase 3A)

- [ ] **3.3.1** Sipariş kartı (konuşma yanında müşterinin son siparişi — Trendyol/HB'den)
- [ ] **3.3.2** Basit escalation notu (devredince AI özet bırakır)
- [ ] **3.3.3** E-ticaret intent'lerine özel cevap kalitesi artırma (Knowledge ile)
- [ ] **3.3.4** DB:
  ```sql
  suggested_replies (id, tenant_id, conversation_id, intent, reply_text, was_accepted, created_at)
  ```

### GR-3.5: Onboarding Otomasyonu (5-10 müşteriye ölçek)

> **Servis:** Backend + Dashboard
> **Kaynak:** eski GR-2.5

- [ ] **3.5.1** Self-service Trendyol/HB API key girişi
- [ ] **3.5.2** Basit tenant setup wizard
- [ ] **3.5.3** Default intent ayarları (her müşteriye aynı başlangıç seti)
- [ ] **3.5.4** Tenant veri izolasyonu güçlendirme

### GR-3.7: Outbound E-ticaret Senaryoları

> **Servis:** `Invekto.Outbound` genişleme
> **Kaynak:** eski GR-2.7

- [ ] **3.7.1** Sipariş teslim edildi → "Memnun musunuz?" trigger'ı
- [ ] **3.7.2** İade talebi sonrası follow-up (T+24h)
- [ ] **3.7.3** B2B lead algılandığında sales alert (email/webhook)
- [ ] **3.7.4** Yorum geldi → otomatik mesaj prep
- [ ] **3.7.5** Tenant-bazlı trigger konfigürasyonu

### GR-3.8: İade Çevirme v1

> **Servis:** `ChatAnalysis` intent + Backend
> **Kaynak:** eski GR-2.8

- [ ] **3.8.1** "İade etmek istiyorum" intent'i algıla
- [ ] **3.8.2** Neden sor (kalite/beden/renk/hasarlı/fikrini değiştirdi)
- [ ] **3.8.3** Nedene göre aksiyon:
  - Beden/renk → değişim öner
  - Fikrini değiştirdi → kupon/indirim öner
  - Kalite/hasar → iade sürecini başlat
- [ ] **3.8.4** Basit conversion tracking (çevrildi/çevrilemedi)
- [ ] **3.8.5** DB:
  ```sql
  return_deflections (id, tenant_id, conversation_id, original_intent, reason_category, action_taken, was_deflected, created_at)
  ```

---

## Gereksinimler — Diş Kliniği

### GR-3.9: Diş Intent + Fiyat Pipeline (3→8-10 intent)

> **Servis:** `ChatAnalysis` genişleme + Automation flow builder
> **Kaynak:** eski GR-2.9 (+ GR-1.5)

- [ ] **3.9.1** Feedback analizi: hastalar en çok ne soruyor?
- [ ] **3.9.2** Yeni intent'ler ekle:
  - [ ] Randevu değiştirme/iptal
  - [ ] Tedavi bilgisi ("İmplant ne kadar sürer?")
  - [ ] Acil durum ("Ağrım var") → doktor alert
  - [ ] Sigorta sorusu ("SGK karşılıyor mu?")
  - [ ] Adres/ulaşım → konum + yol tarifi
  - [ ] Çalışma saatleri
- [ ] **3.9.3** Confidence threshold (düşük güven → sekretere devret)

### GR-3.10: Diş Onboarding Otomasyonu (5+ klinik)

> **Servis:** Backend + Dashboard
> **Kaynak:** eski GR-2.11

- [ ] **3.10.1** Template özelleştirme (klinik adı, doktor adı)
- [ ] **3.10.2** Tenant veri izolasyonu

### GR-3.11: Klinik Outbound v1

> **Servis:** `Invekto.Outbound` genişleme
> **Kaynak:** eski GR-2.12

- [ ] **3.11.1** Randevu hatırlatma otomasyonu (cron → Outbound Engine'e taşı)
- [ ] **3.11.2** Kontrol randevusu hatırlatma (tedavi sonrası T+30 gün)
- [ ] **3.11.3** Doğum günü / yıldönümü mesajı (basit template)
- [ ] **3.11.4** Opt-out yönetimi

---

## Gereksinimler — Estetik Klinik

### GR-3.12: Estetik Intent + Lead Pipeline (3→10-12 intent)

> **Servis:** `ChatAnalysis` genişleme + Backend + Dashboard
> **Kaynak:** eski GR-2.13 (+ GR-1.7)

- [ ] **3.12.1** Yeni intent'ler ekle:
  - [ ] Before/after fotoğraf talebi
  - [ ] İşlem detayı ("Botox ne kadar sürer?")
  - [ ] Kontrendikasyon ("Hamilelikte yapılır mı?")
  - [ ] İyileşme süreci ("Ne zaman normal hayata dönebilirim?")
  - [ ] Paket sorusu ("Botox + dolgu paketi var mı?")
  - [ ] Yabancı hasta → dil algılama + İngilizce cevap (Phase 2 multi-lang ile)
  - [ ] Referans ("Arkadaşım geldi, bana indirim var mı?")
  - [ ] Ödeme/taksit ("Taksit yapılır mı?")
- [ ] **3.12.2** Confidence threshold

### GR-3.13: Lead Management v2

> **Servis:** Backend + Dashboard
> **Kaynak:** eski GR-2.14

- [ ] **3.13.1** Lead source tracking (Instagram, Google, referans, organik)
- [ ] **3.13.2** Lead scoring (basit: ilgi seviyesi + bütçe + zaman)
- [ ] **3.13.3** Pipeline view (yeni → iletişim → konsültasyon → randevu → hasta)
- [ ] **3.13.4** Follow-up otomasyonu (T+24h, T+72h, T+7gün)
- [ ] **3.13.5** "Sıcak lead" alert (yüksek skor → hemen ara)
- [ ] **3.13.6** Lead → randevu → hasta dönüşüm funnel dashboard
- [ ] **3.13.7** DB:
  ```sql
  leads (id, tenant_id, phone, name, source, utm_source, utm_medium, utm_campaign, interest, score, pipeline_status, assigned_to, last_contact_at, next_followup_at, created_at, updated_at)
  lead_activities (id, lead_id, tenant_id, activity_type, note, created_at)
  service_catalog (id, tenant_id, service_name, category, price_min, price_max, duration_minutes, recovery_days, description_tr, description_en, is_active, created_at, updated_at)
  ```

---

## Gereksinimler — Platform (Bağımlı)

### GR-3.16: Negatif Yorum Kurtarma (Senaryo S1)

> **Servis:** `Integrations` + `Outbound`
> **Sektör:** E-ticaret
> **Kaynak:** eski GR-3.5
> **Bağımlılık:** GR-3.4 (Trendyol/HB API — Phase 3A)

- [ ] **3.16.1** Trendyol Review API entegrasyonu (1-2 yıldız yorum tespiti)
- [ ] **3.16.2** Otomatik tetikleme: yorum geldi → AI mesaj hazırla
- [ ] **3.16.3** Mesaj akışı:
  - T+0: "Memnuniyetsizliğiniz için özür dileriz. Ne yapabiliriz?"
  - Çözüm kabul → yorum güncelleme ricası
  - T+48h: Cevap yoksa 1 kez daha dene
- [ ] **3.16.4** Yorum recovery tracking (kurtarılan/kurtarılamayan)
- [ ] **3.16.5** DB:
  ```sql
  review_alerts (id, tenant_id, provider, external_review_id, rating, review_text, customer_phone, recovery_status, created_at, updated_at)
  ```

### GR-3.17: İade Çevirme v2 (S3 genişleme)

> **Servis:** `AgentAI` + `Outbound` + `Integrations`
> **Sektör:** E-ticaret
> **Kaynak:** eski GR-3.6
> **Bağımlılık:** GR-3.8 (İade Çevirme v1 — 3B iç bağımlılık)

- [ ] **3.17.1** Otomatik kupon oluşturma (tenant tanımlı limitler içinde)
- [ ] **3.17.2** Değişim stok kontrolü (Integrations'tan stok sorgula)
- [ ] **3.17.3** İade çevirme başarı oranı (%): çevrilen / toplam iade
- [ ] **3.17.4** Kurtarılan gelir dashboard'da göster
- [ ] **3.17.5** Follow-up (T+24h: "Değişim ürününüz yolda, memnun musunuz?")

### GR-3.24: Proactive Review Rescue (Olumsuz Yorum Önleme)

> **Servis:** `ChatAnalysis` + `AgentAI` + `Outbound` genişletme
> **Sektör:** E-ticaret (primer), tüm sektörler
> **Kaynak:** [../../ideas/review-rescue-ai.md](../../ideas/review-rescue-ai.md)
> **İlişki:** GR-3.8 (İade Çevirme v1) + GR-3.16 (Negatif Yorum Kurtarma) genişletme

- [ ] **3.24.1** Sentiment bazlı risk skoru hesaplama:
  - risk_score = f(sentiment, keywords, timing, response_delay, history)
  - LOW (0-30), MEDIUM (30-60), HIGH (60-80), CRITICAL (80-100)
- [ ] **3.24.2** Keyword algılama: "iade", "şikayet", "yorum yazacağım" → risk artırıcı
- [ ] **3.24.3** Risk seviyesine göre otomatik aksiyon:
  - MEDIUM → agent'a "öncelikli" uyarısı + önerilen cevap
  - HIGH → otomatik özür + çözüm seçenekleri (indirim/iade/değişim)
  - CRITICAL → supervisor + mağaza sahibine alert + VIP etiket
- [ ] **3.24.4** Kurtarma stratejileri (tenant yapılandırılabilir):
  - Özür + empati, indirim kodu, ücretsiz kargo iade, hızlı değişim, tam iade
  - Aylık kurtarma bütçesi limiti (tenant ayarlar)
- [ ] **3.24.5** Follow-up: T+24h "Memnun kaldınız mı?" → T+48h "Bizi değerlendirir misiniz?"
- [ ] **3.24.6** Kurtarma dashboard: kurtarılan yorum sayısı, başarı oranı, korunan satış geliri
- [ ] **3.24.7** DB:
  ```sql
  review_risks (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    conversation_id UUID NOT NULL,
    customer_phone VARCHAR(20),
    risk_score INT NOT NULL,
    risk_level VARCHAR(20) NOT NULL,
    trigger_reason TEXT,
    rescue_status VARCHAR(20) DEFAULT 'pending',
    rescue_strategy VARCHAR(50),
    rescue_cost DECIMAL(10,2),
    customer_response VARCHAR(20),
    review_posted BOOLEAN DEFAULT FALSE,
    review_rating INT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    resolved_at TIMESTAMPTZ
  );

  rescue_templates (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    risk_level VARCHAR(20) NOT NULL,
    strategy VARCHAR(50) NOT NULL,
    message_template TEXT NOT NULL,
    max_discount_pct INT,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
  );
  ```

---

## Gereksinimler — Sağlık Genişleme (v4.2)

> **v4.2 (2026-02-15):** Phase 5'ten taşınan sağlık GR'ları.
> Bu GR'ların teknik bağımlılığı sadece Outbound (Phase 1 ✅) + Randevu Core (Phase 2) + Multi-lang (Phase 2).
> Randevu Advanced (GR-3.19 — Phase 3A) tamamlanmışsa daha güçlü çalışır.

### GR-3.20: Tedavi Sonrası Takip Otomasyonu

> **Servis:** `AgentAI` + `Outbound`
> **Sektör:** Sağlık (Diş + Estetik)
> **Kaynak:** eski GR-5.7 (v4.2 — Phase 5'ten taşındı, teknik bağımlılık yok)

- [ ] **3.20.1** Tedavi tamamlandı → T+1 gün "Nasıl hissediyorsunuz?"
- [ ] **3.20.2** T+7 gün kontrol soruları (ağrı, şişlik, vs.)
- [ ] **3.20.3** T+30 gün "Kontrol randevusu alalım mı?"
- [ ] **3.20.4** Şikayet varsa → doktora alert (acil/normal sınıflandırma)
- [ ] **3.20.5** Takip compliance tracking (hasta cevapladı mı?)
- [ ] **3.20.6** DB:
  ```sql
  treatment_followups (id, tenant_id, patient_phone, treatment_type, followup_day, message_sent, patient_responded, complaint_detected, doctor_alerted, created_at)
  ```

---

### GR-3.21: Google Yorum + Referans Motoru

> **Servis:** `Outbound` + `Integrations`
> **Sektör:** Sağlık (Diş + Estetik)
> **Kaynak:** eski GR-5.8 (v4.2 — Phase 5'ten taşındı, teknik bağımlılık yok)

- [ ] **3.21.1** Tedavi başarılı + hasta memnun → yorum rica mesajı
- [ ] **3.21.2** Google Maps review link gönder
- [ ] **3.21.3** Referans kodu üret → "Arkadaşınıza %10 indirim"
- [ ] **3.21.4** Referral tracking (kim kimi getirdi)
- [ ] **3.21.5** Yorum oranı dashboard'da göster (%3 → %15+ hedef)
- [ ] **3.21.6** DB:
  ```sql
  referrals (id, tenant_id, referrer_phone, referee_phone, referral_code, discount_pct, status, created_at)
  review_requests (id, tenant_id, patient_phone, treatment_type, satisfaction_score, review_link_sent, review_posted, platform, created_at)
  ```

---

### GR-3.22: Medikal Turizm Lead Yönetimi (AR hariç)

> **Servis:** `AgentAI` + `Outbound`
> **Sektör:** Estetik
> **Kaynak:** eski GR-5.9 (v4.2 — AR desteği Phase 5'te kalır)
> **Bağımlılık:** Multi-lang TR/EN (Phase 2 GR-2.3) yeterli

- [ ] **3.22.1** Yabancı hasta akışı: fiyat + konaklama + transfer paketi
- [ ] **3.22.2** Döviz bazlı fiyatlandırma (EUR/USD/GBP)
- [ ] **3.22.3** Consultation booking (online muayene slot)
- [ ] **3.22.4** Multi-language follow-up otomasyonu (TR/EN)
- [ ] **3.22.5** DB:
  ```sql
  medical_tourism_leads (id, tenant_id, patient_phone, patient_country, lang, treatment_interest, accommodation_needed, transfer_needed, budget_currency, status, created_at, updated_at)
  ```

**Phase 5'te kalan:**
- ➡️ Arapça template desteği (AR) → GR-5.6

---

### GR-3.25: Multilingual Medical Tourism Assistant (7/24 Çok Dilli)

> **Servis:** `AgentAI` + `Knowledge` + `Outbound` genişletme
> **Sektör:** Estetik (primer), Diş (sekonder)
> **Kaynak:** [../../ideas/multilingual-medical-tourism.md](../../ideas/multilingual-medical-tourism.md)
> **İlişki:** GR-3.22 Medikal Turizm Lead genişletme + GR-2.3 Multi-lang altyapısı

- [ ] **3.25.1** Language Router: dil algılama → uygun pipeline yönlendirme
  - TR → mevcut pipeline
  - EN/AR/RU/DE → Medical Tourism Pipeline
- [ ] **3.25.2** Kültürel uyum katmanı:
  - Arapça: resmi, saygılı, EUR/USD fiyat
  - İngilizce: rahat, profesyonel, GBP/USD
  - Rusça: detaylı, teknik, EUR
  - Almanca: formal, sertifika odaklı, EUR
- [ ] **3.25.3** Medical Tourism Engine:
  - Intent + entity çıkarma (tedavi, bütçe, tarih, ülke)
  - Klinik tedavi kataloğu + paket bilgisi (RAG)
  - Hastanın dilinde cevap oluştur
  - Döviz çevirisi (EUR/USD/GBP)
- [ ] **3.25.4** Klinik personel görünümü:
  - Orijinal mesaj (yabancı dilde) + Türkçe çeviri + AI cevabı
  - Lead skoru (sıcak/soğuk)
- [ ] **3.25.5** 7/24 otomatik yanıt (gece/tatil/mesai dışı)
  - Voice AI (GR-3.23) ile sinerji: sesli mesaj → transkript → çok dilli cevap
- [ ] **3.25.6** Desteklenen diller: EN + AR (MVP), RU + DE (sonrası)
- [ ] **3.25.7** DB:
  ```sql
  medical_tourism_conversations (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    patient_phone VARCHAR(20),
    patient_country VARCHAR(3),
    patient_lang VARCHAR(5),
    treatment_interest VARCHAR(200),
    package_interest JSONB,
    lead_score INT,
    auto_responded BOOLEAN DEFAULT FALSE,
    response_lang VARCHAR(5),
    created_at TIMESTAMPTZ DEFAULT NOW()
  );
  ```

---

## Gereksinimler — Evrensel AI (v4.5)

### GR-3.23: Voice Message AI (Sesli Mesaj Transkript + Intent)

> **Servis:** Yeni modül (`Invekto.Automation` + `Invekto.AgentAI` genişletme)
> **Sektör:** Tümü (e-ticaret, diş, estetik, otel — evrensel)
> **Kaynak:** [../../ideas/voice-message-ai.md](../../ideas/voice-message-ai.md)

- [ ] **3.23.1** Whisper API entegrasyonu (sesli mesaj → transkript):
  - OGG/MP3 → WAV dönüşümü (FFmpeg)
  - Çoklu dil algılama (TR/EN/AR/RU/DE)
  - Gürültü toleranslı transkript
- [ ] **3.23.2** Mevcut AgentAI pipeline'ına transkript aktar:
  - Sesli mesaj → transkript → yazılı mesaj gibi davran
  - Multi-intent algılama (sesli mesajlar genelde çoklu konu)
- [ ] **3.23.3** Automation trigger: sesli mesaj geldi → flow tetikle
  - Flow Builder'da "Sesli Mesaj" trigger node'u
- [ ] **3.23.4** Agent UI: sesli mesajın yanında transkript + intent gösterimi
- [ ] **3.23.5** Sentiment analizi: ses tonundan duygu algılama (kızgın/memnun/nötr/acil)
  - Acil/kızgın → öncelikli queue
- [ ] **3.23.6** Güven skoru düşükse: "Anlayamadım, yazılı gönderir misiniz?"
- [ ] **3.23.7** DB:
  ```sql
  voice_transcripts (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    conversation_id UUID NOT NULL,
    audio_duration_sec INT,
    language VARCHAR(5),
    transcript TEXT,
    confidence FLOAT,
    intents JSONB,
    sentiment VARCHAR(20),
    created_at TIMESTAMPTZ DEFAULT NOW()
  );
  ```

---

## Gereksinimler — Cross-Sektör Boşluk (v6)

> **v6 (2026-02-16):** 5 AI review raporunun tespit ettiği cross-sektör eksiklikler.
> Bu GR'lar tüm sektörleri etkiler. Detay: `ideas/scenarios-review-actions.md` → B1 bölümü.

### GR-3.30: AI→İnsan Handoff (Eskalasyon Kuralları)

> **Servis:** `AgentAI` + Backend
> **Kaynak:** CS-02 (B1.2)
> **Paket:** PKT-6A
> **Etki:** BLOCKER — bu olmadan AI güvenilir değildir

- [ ] **3.30.1** Handoff tetikleyicileri:
  - AI confidence < threshold (örnek: %60)
  - Belirli intent'ler (tıbbi tavsiye, hukuki, fiyat kesinleştirme)
  - Müşteri açıkça "insanla konuşmak istiyorum" dediğinde
  - Sentiment skoru kritik eşiği aştığında
  - Aynı konuda 3+ mesaj döngüsü (AI çözemiyor)
- [ ] **3.30.2** Context aktarımı: AI'nin topladığı bilgi (intent, sentiment, profil, özet) insana transfer
- [ ] **3.30.3** Handoff UX: müşteriye "sizi uzman arkadaşımıza yönlendiriyorum" mesajı
- [ ] **3.30.4** Geri dönüş kaydı: insan çözdükten sonra AI özete kayıt (knowledge loop)

---

### GR-3.31: AI Hallucination Guardrail

> **Servis:** `AgentAI` genişleme
> **Kaynak:** CS-03 (B1.3)
> **Paket:** PKT-6A
> **Etki:** YÜKSEK — yasal risk azaltma

- [ ] **3.31.1** "Bilmiyorum" yeteneği: emin olmadığı konuda → "Bu konuda kesin bilgi veremiyorum, uzmanımıza yönlendiriyorum"
- [ ] **3.31.2** Konu bazlı guardrail listesi (tenant yapılandırılabilir):
  - Tıbbi tavsiye → ASLA kesin diagnosis
  - Fiyat → "aralık" ver, "kesin fiyat görüşmede belirlenir"
  - İlaç/dozaj → ASLA öneri, doktora yönlendir
  - Hukuki (iade hakkı, garanti) → Knowledge'dan kaynak göster
- [ ] **3.31.3** Confidence-based routing: düşük confidence → human handoff (GR-3.30 ile entegre)
- [ ] **3.31.4** Audit log: AI'nin verdiği her cevabın kaydı + confidence skoru

---

### GR-3.32: Churn Sinyali Tespiti

> **Servis:** `ChatAnalysis` + `AgentAI`
> **Kaynak:** CS-05 (B1.5)
> **Paket:** PKT-6B
> **Etki:** ORTA-YÜKSEK — retention artışı

- [ ] **3.32.1** Churn sinyal pattern'leri:
  - Pasif agresif: "neyse", "boş ver", "bir daha uğraşmam"
  - Karşılaştırma: "rakip X daha ucuz", "başka yere bakıyorum"
  - Soğuma: 3+ gün cevap yok (aktif konuşmada)
  - Frekans düşüşü: düzenli müşteri → uzun süre sipariş/randevu yok
- [ ] **3.32.2** Risk skoru: LOW / MEDIUM / HIGH / CRITICAL
- [ ] **3.32.3** Otomatik aksiyon:
  - MEDIUM → agent'e "dikkat: kayıp riski" badge
  - HIGH → supervisor'a alert + önerilen kurtarma
  - CRITICAL → Outbound kurtarma mesajı (özel teklif)
- [ ] **3.32.4** Dashboard: churn risk pipeline
- [ ] **3.32.5** DB:
  ```sql
  churn_signals (id, tenant_id, customer_phone, signal_type, signal_text, risk_score, risk_level, action_taken, created_at)
  ```

---

### GR-3.33: Unified Customer Timeline

> **Servis:** Backend + Dashboard
> **Kaynak:** CS-06 (B1.6)
> **Paket:** PKT-6B
> **Etki:** YÜKSEK — tüm AI ve routing kalitesini artırır

- [ ] **3.33.1** Müşteri profili birleştirme: telefon + email + IG handle + WA numara eşleştirme
- [ ] **3.33.2** Timeline görünümü: kronolojik, kanal ikonu ile (WA/IG/telefon/email/sipariş/randevu)
- [ ] **3.33.3** Her entry: kanal, tarih, konu/intent, çözüm durumu, agent
- [ ] **3.33.4** AI context window: son 10 etkileşim özeti → cevap önerisi için
- [ ] **3.33.5** CRM entegrasyonu: sipariş geçmişi, randevu geçmişi, yorum geçmişi
- [ ] **3.33.6** DB:
  ```sql
  customer_profiles (id, tenant_id, phone, email, ig_handle, name, merged_from_ids, created_at, updated_at)
  customer_timeline (id, tenant_id, profile_id, channel, event_type, event_data_json, intent, agent_id, created_at)
  ```

---

### GR-3.34: Revenue Attribution

> **Servis:** Backend + Dashboard
> **Kaynak:** CS-07 (B1.7)
> **Paket:** PKT-6B
> **İlişki:** GR-3.14 (Ads Attribution) üzerine genişleme
> **Etki:** YÜKSEK — enterprise satış için şart

- [ ] **3.34.1** Conversion source tracking: ilk temas kanalı (WA organic, IG ad, Google, referral)
- [ ] **3.34.2** AI vs Human flag: cevabı AI mi önerdi, insan mı yazdı, ikisi birlikte mi
- [ ] **3.34.3** Deal value: randevu → tedavi tutarı, sipariş → sepet tutarı
- [ ] **3.34.4** Funnel: lead → first response → qualified → appointment/purchase → closed
- [ ] **3.34.5** Dashboard: kanal bazlı ROI, agent bazlı kapanış oranı, AI assist oranı

---

## Gereksinimler — E-ticaret Boşluk (v6)

> **v6 (2026-02-16):** E-ticaret sektöründe tespit edilen eksik senaryolar.
> Detay: `ideas/scenarios-review-actions.md` → B2 bölümü.

### GR-3.35: Stok Bildirim (Back-in-Stock)

> **Servis:** `Invekto.Outbound` + `Invekto.Integrations`
> **Kaynak:** EB-01 (B2.1)
> **Paket:** PKT-6B
> **Etki:** ORTA — müşteri memnuniyeti + dönüşüm

- [ ] **3.35.1** "Gelince haber ver" intent algılama
- [ ] **3.35.2** Stok izleme: Integrations'tan stok girişi tespiti
- [ ] **3.35.3** Otomatik WA bildirim: stok girdi → opt-in müşteriye mesaj
- [ ] **3.35.4** Template kategorisi: utility (opt-in gerekli → GR-3.26)

---

### GR-3.36: Influencer/Affiliate Attribution

> **Servis:** Backend + Dashboard
> **Kaynak:** EB-02 (B2.2)
> **Paket:** PKT-6B
> **İlişki:** GR-3.14 (Ads Attribution) genişleme
> **Etki:** ORTA — pazarlama ROI

- [ ] **3.36.1** Influencer kodu tanımlama (tenant bazlı)
- [ ] **3.36.2** UTM + kupon kodu ile kampanya etiketleme
- [ ] **3.36.3** Attribution dashboard: hangi influencer ne kadar satış getirdi
- [ ] **3.36.4** "Kod neydi?" intent → son kullanılan kodu göster

---

### GR-3.37: Cross-Platform Sipariş Eşleştirme

> **Servis:** `Invekto.Integrations` + Backend
> **Kaynak:** EB-04 (B2.4)
> **Paket:** PKT-6B
> **Bağımlılık:** GR-3.4 (Integrations — Phase 3A)
> **Etki:** ORTA — operasyonel verimlilik

- [ ] **3.37.1** Telefon numarası ile cross-platform eşleştirme (Trendyol + HB)
- [ ] **3.37.2** Müşteri hangi siparişi soruyorsa otomatik tespit
- [ ] **3.37.3** Agent Assist'te birden fazla platform siparişi gösterme

---

### GR-3.38: Şikayetvar Eskalasyon

> **Servis:** `Invekto.Outbound` + Backend
> **Kaynak:** EB-05 (B2.5)
> **Paket:** PKT-6B
> **Etki:** ORTA — itibar koruma

- [ ] **3.38.1** Şikayetvar entegrasyonu (web scraping veya API — araştır)
- [ ] **3.38.2** Yeni şikayet tespiti → tenant'a alert
- [ ] **3.38.3** Proaktif WA mesajı: "Şikayetinizi gördük, hemen çözmek istiyoruz"
- [ ] **3.38.4** Çözüm tracking: şikayet kapatıldı mı, müşteri memnun mu

---

### GR-3.39: Garanti ve Teknik Servis

> **Servis:** `Invekto.Knowledge` + `Invekto.Automation`
> **Kaynak:** EB-06 (B2.6)
> **Paket:** PKT-6A
> **Etki:** DÜŞÜK-ORTA

- [ ] **3.39.1** Garanti sorusu intent: "Bozuldu", "Garanti kapsamında mı?"
- [ ] **3.39.2** Garanti süresi kontrolü (sipariş tarihi + garanti süresi → Knowledge)
- [ ] **3.39.3** Teknik servis yönlendirme: adres, telefon, süreç bilgisi (Knowledge)
- [ ] **3.39.4** Automation flow: garanti intent → bilgi toplama → yönlendirme

---

### GR-3.40: Fraud / Dolandırıcılık Şüphesi

> **Servis:** Backend + `AgentAI`
> **Kaynak:** EB-07 (B2.7)
> **Paket:** PKT-6A
> **Etki:** YÜKSEK — güvenlik

- [ ] **3.40.1** Fraud intent algılama: "Bu siparişi ben vermedim", "Hesabım çalındı"
- [ ] **3.40.2** PRIORITY routing: normal kuyruk bypass → acil agent/supervisor
- [ ] **3.40.3** Otomatik hesap dondurma önerisi (agent onayı ile)
- [ ] **3.40.4** Fraud log: tenant + müşteri + olay detayı kaydı

---

## Gereksinimler — Sağlık Boşluk (v6)

> **v6 (2026-02-16):** Sağlık sektöründe tespit edilen eksik senaryolar.
> Detay: `ideas/scenarios-review-actions.md` → B3 bölümü.

### GR-3.41: Tedavi Planı Onay Akışı

> **Servis:** `Invekto.Outbound` + `Invekto.AgentAI`
> **Kaynak:** SB-01 (B3.1)
> **Paket:** PKT-6C
> **Sektör:** Sağlık (Diş + Estetik)
> **Etki:** YÜKSEK — kaybedilen 15-50K TL/tedavi

- [ ] **3.41.1** Tedavi planı gönderildi event → follow-up zinciri başlat
- [ ] **3.41.2** T+1 gün: "Tedavi planınızı incelediniz mi?"
- [ ] **3.41.3** T+3 gün: "Sorularınız varsa yardımcı olabiliriz"
- [ ] **3.41.4** T+7 gün: son hatırlatma + özel teklif opsiyonu
- [ ] **3.41.5** Onay gelmezse → supervisor'a alert (kayıp riski)

---

### GR-3.42: Çoklu Klinik/Şube Yönetimi

> **Servis:** Backend + Dashboard
> **Kaynak:** SB-03 (B3.3)
> **Paket:** PKT-6C
> **Sektör:** Sağlık (zincir klinikler)
> **Etki:** ORTA — zincir klinikler için şart

- [ ] **3.42.1** Konum bazlı yönlendirme: "Kadıköy şubemizde Dr. Mehmet Pzt-Çar, Beşiktaş'ta Per-Cum"
- [ ] **3.42.2** Şube bazlı slot yönetimi (GR-3.19 genişleme)
- [ ] **3.42.3** Merkezi dashboard: tüm şubelerin performansı tek ekranda
- [ ] **3.42.4** Hasta→şube eşleştirme (konum/tercih bazlı)

---

### GR-3.43: Tedavi Öncesi Hazırlık Talimatları

> **Servis:** `Invekto.Outbound` + `Invekto.Knowledge`
> **Kaynak:** SB-04 (B3.4)
> **Paket:** PKT-6C
> **Sektör:** Sağlık (Diş + Estetik)
> **İlişki:** S8'in tersi — S8 post-op, bu pre-op
> **Etki:** YÜKSEK — hazırlık eksikse ameliyat iptal → koltuk boş

- [ ] **3.43.1** Tedavi tipine göre hazırlık talimatı template (Knowledge):
  - Ameliyat: 8 saat açlık, X ilacı kesin, refakatçi
  - İmplant: antibiyotik başlat, oral hijyen
  - Estetik: güneşten korunma, kan sulandırıcı kesin
- [ ] **3.43.2** Outbound trigger: randevu T-3gün, T-1gün, T-sabah mesaj zinciri
- [ ] **3.43.3** Hazırlık onay: hasta "okudum" dedi mi tracking

---

### GR-3.44: Reçete/İlaç Sorguları

> **Servis:** `Invekto.Knowledge` + `Invekto.AgentAI`
> **Kaynak:** SB-05 (B3.5)
> **Paket:** PKT-6C
> **Sektör:** Sağlık
> **Etki:** DÜŞÜK-ORTA
> **DİKKAT:** Dozaj önerisi YAPMA, sadece doktorun verdiği bilgiyi tekrarla (GR-3.31 guardrail)

- [ ] **3.44.1** Reçete sorusu intent: "Reçetemi yazdınız mı?", "İlacı nereden alacağım?"
- [ ] **3.44.2** Knowledge'dan otomatik cevaplama: eczane bilgisi, genel kullanım talimatı
- [ ] **3.44.3** Dozaj sorusu → guardrail: "Bu bilgiyi doktorunuzla doğrulayın" uyarısı
- [ ] **3.44.4** Reçete hatırlatma: T+X gün "İlacınız bitiyor olabilir, kontrol randevusu alalım mı?"

---

## Çıkış Kriterleri (Phase 4'e Geçiş Şartı)

- [ ] E-ticaret: 15+ aktif ödeyen müşteri
- [ ] Diş: 7+ aktif ödeyen klinik
- [ ] Estetik: 5+ aktif ödeyen klinik
- [ ] Deflection rate %50+ (e-ticaret, Knowledge ile)
- [ ] Fiyat→randevu dönüşüm %35+ (diş)
- [ ] Lead→randevu dönüşüm %35+ (estetik)
- [ ] Outbound conversion rate %5+ (mesaj → aksiyon)
- [ ] İade çevirme oranı %15+ (çevrilen / toplam iade)
- [ ] En az 1 B2B lead yakalandı
- [ ] Tedavi takip otomasyonu çalışıyor, en az 2 klinik aktif (v4.2)
- [ ] Google yorum oranı artışı ölçülüyor (v4.2)
- [ ] Medikal turizm lead akışı çalışıyor (EN, AR hariç) (v4.2)
- [ ] Voice AI: sesli mesaj transkript çalışıyor, en az 5 tenant aktif (v4.5)
- [ ] Review Rescue: kurtarma başarı oranı %60+ (v4.5)
- [ ] Multilingual: EN + AR otomatik yanıt çalışıyor, en az 3 klinik aktif (v4.5)
- [ ] Kurumsal talepler geliyor → "SSO var mı? Audit log var mı?"

### Niche Bazlı Başarı Kriterleri

| Kriter | E-ticaret | Diş | Estetik |
|--------|-----------|-----|---------|
| Yeni müşteri | 5-10 satıcı | 3-5 klinik | 3-5 klinik |
| Niche MRR katkısı | 15-50K TL | 22-37K TL | 45-75K TL |
| Deflection rate | %40+ | N/A | N/A |
| Dönüşüm | İade çevirme %15+ | Fiyat→randevu %30+ | Lead→randevu %30+ |
| No-show | N/A | %10 altı | N/A |
| Case study | 1 yayınlanabilir | 1 yayınlanabilir | 1 yayınlanabilir |

---

## Notlar

- v4.3'te Phase 3'ten bölündü (16/22 GR — niche derinleştirme), v6'da 34 GR'a çıktı (+15 CS/EB/SB)
- Phase 2'deki RAG/Knowledge altyapısı sayesinde tüm intent'ler doğru bilgiyle çalışır
- Phase 3A'daki Integrations, Outbound v2, Dashboard buradaki GR'ların temelini oluşturur
- Multi-language desteği Phase 2'de kurulmuş, burada niche-özel template'ler eklenir
- Otel niche: PMS entegrasyonu Integrations'a eklenebilir (talep varsa)
- **v4.2:** Sağlık genişleme GR'ları (3.20-3.22) Phase 5'ten taşındı — sağlık niche'e erken değer
- **v4.2:** Ads Attribution artık Phase 3A'da (GR-3.14)
- **v4.5:** 3 yeni GR eklendi:
  - GR-3.23 Voice Message AI — tüm sektörlere sesli mesaj transkript + intent ([ideas/voice-message-ai.md](../../ideas/voice-message-ai.md))
  - GR-3.24 Proactive Review Rescue — yorum yazılmadan önce müdahale ([ideas/review-rescue-ai.md](../../ideas/review-rescue-ai.md))
  - GR-3.25 Multilingual Medical Tourism — 7/24 çok dilli medikal turizm asistanı ([ideas/multilingual-medical-tourism.md](../../ideas/multilingual-medical-tourism.md))
