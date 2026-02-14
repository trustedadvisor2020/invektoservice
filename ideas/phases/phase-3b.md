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

---

## Durum Takibi

| Alt Gereksinim | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------------------|--------|
| **E-TİCARET** | | | |
| GR-3.1 Intent Genişletme + Oto. Etiketleme | ⬜ Başlamadı | — | ← eski GR-2.1 |
| GR-3.2 B2B / VIP Lead Tespiti | ⬜ Başlamadı | — | ← eski GR-2.2 |
| GR-3.3 Agent Assist Genişleme (E-ticaret) | ⬜ Başlamadı | — | Integrations (3A) gerekli |
| GR-3.5 Onboarding Otomasyonu | ⬜ Başlamadı | — | ← eski GR-2.5 |
| GR-3.7 Outbound E-ticaret Senaryoları | ⬜ Başlamadı | — | ← eski GR-2.7 |
| GR-3.8 İade Çevirme v1 | ⬜ Başlamadı | — | ← eski GR-2.8 |
| **DİŞ KLİNİĞİ** | | | |
| GR-3.9 Diş Intent + Fiyat Pipeline | ⬜ Başlamadı | — | ← eski GR-2.9 |
| GR-3.10 Diş Onboarding Otomasyonu | ⬜ Başlamadı | — | ← eski GR-2.11 |
| GR-3.11 Klinik Outbound v1 | ⬜ Başlamadı | — | ← eski GR-2.12 |
| **ESTETİK KLİNİK** | | | |
| GR-3.12 Estetik Intent + Lead Pipeline | ⬜ Başlamadı | — | ← eski GR-2.13 |
| GR-3.13 Lead Management v2 | ⬜ Başlamadı | — | ← eski GR-2.14 |
| **PLATFORM (BAĞIMLI)** | | | |
| GR-3.16 Negatif Yorum Kurtarma | ⬜ Başlamadı | — | Bağımlılık: GR-3.4 (3A) |
| GR-3.17 İade Çevirme v2 | ⬜ Başlamadı | — | Bağımlılık: GR-3.8 (3B iç) |
| GR-3.24 Proactive Review Rescue | ⬜ Başlamadı | — | GR-3.8/3.16 genişletme, proaktif kurtarma |
| **SAĞLIK GENİŞLEME** | | | |
| GR-3.20 Tedavi Sonrası Takip | ⬜ Başlamadı | — | ← eski GR-5.7 (v4.2) |
| GR-3.21 Google Yorum + Referans Motoru | ⬜ Başlamadı | — | ← eski GR-5.8 (v4.2) |
| GR-3.22 Medikal Turizm Lead (AR hariç) | ⬜ Başlamadı | — | ← eski GR-5.9 (v4.2, AR → Phase 5) |
| GR-3.25 Multilingual Medical Tourism | ⬜ Başlamadı | — | GR-3.22 genişletme, 7/24 çok dilli asistan |
| **EVRENSEL AI** | | | |
| GR-3.23 Voice Message AI | ⬜ Başlamadı | — | Whisper transkript + mevcut AgentAI pipeline |

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

- v4.3'te Phase 3'ten bölündü (16/22 GR — niche derinleştirme)
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
