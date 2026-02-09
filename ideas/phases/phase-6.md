# Phase 6 — Operasyon & Analytics: QA + Conversation Mining

> **Hafta:** 41-48
> **MRR Hedefi:** 2M+ TL
> **Müşteri Hedefi:** 200+
> **Bağımlılık:** Phase 5 tamamlanmış olmalı
> **Durum:** ⬜ Başlamadı

---

## Durum Takibi

| Alt Gereksinim | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------------------|--------|
| GR-6.1 SLA Tracker (tam) | ⬜ Başlamadı | — | — |
| GR-6.2 QA Scoring (C13) | ⬜ Başlamadı | — | — |
| GR-6.3 Conversation Mining (C13) | ⬜ Başlamadı | — | — |
| GR-6.4 Knowledge Gap Report | ⬜ Başlamadı | — | — |
| GR-6.5 Revenue Attribution Dashboard | ⬜ Başlamadı | — | — |

---

## Özet

"Ekip performansını ölç, büyürken kontrolü kaybetme, AI'ı sürekli iyileştir." Phase 4'te hazırlık yapıldı (metadata log, script compliance). Şimdi tam kapsamlı kalite kontrol + mining.

**Satış dili:** "Dashboard'dan her şeyi izle: yanıt süreleri, temsilci kalitesi, gelir etkisi"

---

## Gereksinimler

### GR-6.1: SLA Tracker (tam)

> **Servis:** Backend + Dashboard genişleme
> **Sektör:** Tümü

- [ ] **6.1.1** First response time (FRT) + resolution time ölçümü
- [ ] **6.1.2** Tenant bazlı SLA hedefleri tanımlama
  - Örnek: FRT <2dk, resolution <30dk
- [ ] **6.1.3** Breach alerts (email + webhook + dashboard bildirim)
- [ ] **6.1.4** SLA compliance raporu (günlük/haftalık/aylık)
- [ ] **6.1.5** Eskalasyon kuralları (SLA breach → supervisor → manager)
- [ ] **6.1.6** Niche-özel SLA'lar:
  - Sağlık: acil mesaj <5dk
  - E-ticaret: kargo sorusu <15dk
- [ ] **6.1.7** DB:
  ```sql
  sla_configs (id, tenant_id, channel, priority, frt_target_sec, resolution_target_sec, escalation_rules_json, created_at, updated_at)
  sla_breaches (id, tenant_id, conversation_id, breach_type, target_sec, actual_sec, escalated_to, created_at)
  ```

---

### GR-6.2: QA Scoring (C13 — tam)

> **Servis:** `ChatAnalysis` genişleme + Dashboard
> **Sektör:** Tümü

- [ ] **6.2.1** AI destekli temsilci değerlendirme (her konuşma sonrası otomatik skor)
- [ ] **6.2.2** Skor kriterleri:
  - [ ] Script compliance (doğru akış takip edildi mi?)
  - [ ] Sentiment alignment (müşteri tonu iyileşti mi kötüleşti mi?)
  - [ ] Resolution quality (sorun çözüldü mü? tekrar mı yazıldı?)
  - [ ] Response time compliance (SLA'ya uyuldu mu?)
  - [ ] Knowledge accuracy (bilgi doğru verildi mi?)
- [ ] **6.2.3** Manager review queue (düşük skorlu konuşmalar → inceleme)
- [ ] **6.2.4** Agent coaching insights
  - "Temsilci X: empati dili zayıf, kriz yönetimi güçlü"
- [ ] **6.2.5** Niche-özel skor ağırlıkları:
  - Sağlık: KVKK compliance + tıbbi disclaimer ağırlıklı
  - E-ticaret: çözüm hızı + iade çevirme ağırlıklı
  - Estetik: lead dönüşüm + takip kalitesi ağırlıklı
- [ ] **6.2.6** Agent bazlı trend raporu (haftalık iyileşme/kötüleşme)
- [ ] **6.2.7** DB:
  ```sql
  qa_scores (id, tenant_id, conversation_id, agent_id, script_compliance, sentiment_alignment, resolution_quality, response_time_compliance, knowledge_accuracy, overall_score, reviewed_by, review_notes, created_at)
  qa_coaching_insights (id, tenant_id, agent_id, period_start, period_end, strengths_json, weaknesses_json, recommendations, created_at)
  ```

---

### GR-6.3: Conversation Mining (C13 — tam)

> **Servis:** `ChatAnalysis` + `Knowledge` + Dashboard
> **Sektör:** Tümü

- [ ] **6.3.1** Win/loss phrase analizi (hangi cümleler satış/randevu getiriyor?)
- [ ] **6.3.2** Top complaint drivers (en çok ne şikayet ediliyor? — haftalık)
- [ ] **6.3.3** Top conversion patterns (satışı kapatan kalıplar → en iyi template'e dönüştür)
- [ ] **6.3.4** Churn signal detection (müşteri kaybetme riski olan konuşma kalıpları)
- [ ] **6.3.5** Intent trend analizi (yeni ortaya çıkan intent'ler — knowledge gap mı?)
- [ ] **6.3.6** Niche-özel mining:
  - [ ] E-ticaret: en çok iade nedeni, en çok şikayet edilen ürün kategorisi
  - [ ] Diş: en çok sorulan tedavi, fiyat hassasiyeti kalıpları
  - [ ] Estetik: lead kaybetme nedeni, en etkili before/after stratejisi
- [ ] **6.3.7** Haftalık "insight digest" raporu (top 5 bulgu)
- [ ] **6.3.8** DB:
  ```sql
  mining_insights (id, tenant_id, insight_type, period_start, period_end, data_json, created_at)
  mining_digests (id, tenant_id, week_start, top_insights_json, action_items_json, created_at)
  ```

---

### GR-6.4: Knowledge Gap Report (tam)

> **Servis:** `Knowledge` + Dashboard
> **Sektör:** Tümü

- [ ] **6.4.1** Son 7/30 gün top 50 unanswered intents
- [ ] **6.4.2** "Doc ekle" 1-tık aksiyonu (direkt knowledge editor'e yönlendir)
- [ ] **6.4.3** AI accuracy trend (zaman içinde iyileşme grafiği)
- [ ] **6.4.4** Gap→doc öneri sistemi ("Bu soruya cevap yok — şu bilgiyi ekle")
- [ ] **6.4.5** Tenant bazlı knowledge health score (eksik/güncel/eski)
- [ ] **6.4.6** DB:
  ```sql
  knowledge_gaps (id, tenant_id, unanswered_intent, frequency, first_seen, last_seen, status, resolved_doc_id, created_at)
  ```

---

### GR-6.5: Revenue Attribution Dashboard (tam)

> **Servis:** Dashboard genişleme
> **Sektör:** Tümü

- [ ] **6.5.1** Kanal bazlı gelir (WhatsApp / Instagram / Web / referans)
- [ ] **6.5.2** Kampanya bazlı ROI (UTM + Click-to-WA attribution)
- [ ] **6.5.3** Agent bazlı satış performansı (gelir katkısı)
- [ ] **6.5.4** AI vs Human karşılaştırması (AI çözdü mü insan mı?)
- [ ] **6.5.5** Abandoned cart recovery rate ve kurtarılan TL
- [ ] **6.5.6** İade çevirme oranı + kurtarılan TL
- [ ] **6.5.7** Niche-özel dashboard:
  - [ ] E-ticaret: marketplace bazlı performans
  - [ ] Diş: tedavi bazlı randevu→gelir funnel
  - [ ] Estetik: kampanya→lead→hasta tam attribution

---

## Çıkış Kriterleri (Phase 7'ye Geçiş Şartı)

- [ ] SLA compliance %90+ (tüm tenant ortalaması)
- [ ] QA skor ortalaması %75+ (tüm agent'lar)
- [ ] Knowledge gap close rate %60+ (tespit edilen boşluklar kapatılıyor)
- [ ] Conversation mining'den en az 5 actionable insight/ay çıkıyor
- [ ] Platform stabil, ölçek sorunları yok (concurrent user limiti test edilmiş)
- [ ] MRR 2M+ TL
