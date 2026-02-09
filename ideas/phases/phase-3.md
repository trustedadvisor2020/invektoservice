# Phase 3 — AI Derinleştirme (Knowledge Base + Akıllı Agent)

> **Hafta:** 17-24
> **MRR Hedefi:** 500-800K TL
> **Müşteri Hedefi:** 100+
> **Bağımlılık:** Phase 2 tamamlanmış olmalı
> **Durum:** ⬜ Başlamadı

---

## Durum Takibi

| Alt Gereksinim | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------------------|--------|
| GR-3.1 Knowledge Service (RAG) | ⬜ Başlamadı | — | — |
| GR-3.2 AgentAI Orchestrator | ⬜ Başlamadı | — | — |
| GR-3.3 Agent Assist v2 | ⬜ Başlamadı | — | — |
| GR-3.4 Outbound Engine v2 | ⬜ Başlamadı | — | — |
| GR-3.5 Negatif Yorum Kurtarma (S1) | ⬜ Başlamadı | — | — |
| GR-3.6 İade Çevirme v2 (S3) | ⬜ Başlamadı | — | — |
| GR-3.7 Multi-Language AI (TR/EN) | ⬜ Başlamadı | — | — |
| GR-3.8 Dashboard Genişletme | ⬜ Başlamadı | — | — |

---

## Özet

"AI artık şirket verisinden cevap veriyor, sallama yapmıyor." Knowledge base + RAG ile AI doğruluğu artar, agent iş yükü ciddi düşer.

**Satış dili:** "Ürün/tedavi sorularınızı da otomatik cevaplayacağız — kendi verinizle"

**Yeni Mikro Servis:**

| Servis | Port | Sorumluluk |
|--------|------|------------|
| `Invekto.Knowledge` | 7104 | RAG, bilgi tabanı, pgvector embeddings |

**3 niche'e birden serve eder:**
- E-ticaret: Ürün bilgisi, iade politikası, kargo kuralları
- Diş: Tedavi bilgisi, fiyat detayı, sigorta kapsamı
- Estetik: İşlem detayı, kontrendikasyon, iyileşme süreci, multi-language

---

## Gereksinimler

### GR-3.1: Knowledge Service (RAG)

> **Servis:** `Invekto.Knowledge` (port 7104) — YENİ
> **Sektör:** Tümü

- [ ] **3.1.1** Knowledge servis iskeletini oluştur (port 7104, health check, tenant izolasyon)
- [ ] **3.1.2** PDF upload + chunking
  - Ürün kataloğu, SSS dokümanları, politika belgeleri
  - Chunk boyutu + overlap stratejisi
- [ ] **3.1.3** FAQ editor (hızlı soru-cevap girişi — UI)
- [ ] **3.1.4** Embeddings pipeline (pgvector)
  - Embedding model seçimi
  - Tenant bazlı izolasyon (aynı pgvector instance, farklı tenant_id)
- [ ] **3.1.5** Retrieval API (topK + tenant izolasyonu)
  - Soru → en yakın chunk'lar → context oluştur
- [ ] **3.1.6** Kaynak referanslı cevap ("pricing.pdf sayfa 3'e göre...")
- [ ] **3.1.7** DB:
  ```sql
  documents (id, tenant_id, title, source_type, status, created_at, updated_at)
  chunks (id, document_id, tenant_id, content, chunk_index, metadata_json, embedding vector, created_at)
  faqs (id, tenant_id, question, answer, category, lang, created_at, updated_at)
  tags (id, tenant_id, name, created_at)
  document_tags (document_id, tag_id)
  ```

**Yapılmayacak:**
- ❌ URL crawl (PDF + FAQ yeterli başta)
- ❌ Document versioning
- ❌ Knowledge gap report (veri birikmesi lazım)

---

### GR-3.2: AgentAI Orchestrator

> **Servis:** `Invekto.AgentAI` (port 7105) — genişleme
> **Sektör:** Tümü

- [ ] **3.2.1** Pipeline kurulumu: message → intent → knowledge lookup → response → output
- [ ] **3.2.2** Intent sayısını 10 → 15-20'ye çıkar
  - [ ] Mevcut: kargo, iade, sipariş, iptal, fatura, stok, değişim...
  - [ ] YENİ: fiyat sorusu, ürün karşılaştırma, garanti, kampanya
  - [ ] YENİ: genel SSS (iade politikası, teslimat süresi, ödeme yöntemleri)
- [ ] **3.2.3** Kaynak yoksa "insana devret" kuralı
- [ ] **3.2.4** Mevcut ChatAnalysis 15 kriterli analizi korunur (ayrı servis)

---

### GR-3.3: Agent Assist v2

> **Servis:** `AgentAI` + Dashboard UI
> **Sektör:** Tümü

- [ ] **3.3.1** Reply generation artık Knowledge'dan beslenecek
- [ ] **3.3.2** "Neden bu cevap" açıklaması + kaynak referansı
- [ ] **3.3.3** Tone presets (formal / kısa / samimi)
- [ ] **3.3.4** Multi-turn: AI takip sorusu sorabiliyor

---

### GR-3.4: Outbound Engine v2

> **Servis:** `Invekto.Outbound` genişleme
> **Sektör:** Tümü

- [ ] **3.4.1** Campaign yönetimi (kampanya oluştur, hedef kitle seç, zamanlama)
- [ ] **3.4.2** AI-generated personalization (müşteri geçmişine göre mesaj)
- [ ] **3.4.3** Conversion tracking (mesaj → aksiyon: cevap/satın alma/randevu)
- [ ] **3.4.4** A/B testing (2 şablon → hangisi daha iyi dönüyor)
- [ ] **3.4.5** Multi-language template desteği (TR/EN)
- [ ] **3.4.6** Time-based trigger'lar (T+Xh delay, recurring schedule)
- [ ] **3.4.7** ROI dashboard (kampanya bazlı gelir etkisi)
- [ ] **3.4.8** DB:
  ```sql
  outbound_campaigns (id, tenant_id, name, trigger_type, target_criteria_json, template_id, schedule_json, status, stats_json, created_at, updated_at)
  outbound_conversions (id, tenant_id, message_id, campaign_id, conversion_type, value_amount, created_at)
  ```

---

### GR-3.5: Negatif Yorum Kurtarma (Senaryo S1)

> **Servis:** `Integrations` + `Outbound`
> **Sektör:** E-ticaret

- [ ] **3.5.1** Trendyol Review API entegrasyonu (1-2 yıldız yorum tespiti)
- [ ] **3.5.2** Otomatik tetikleme: yorum geldi → AI mesaj hazırla
- [ ] **3.5.3** Mesaj akışı:
  - T+0: "Memnuniyetsizliğiniz için özür dileriz. Ne yapabiliriz?"
  - Çözüm kabul → yorum güncelleme ricası
  - T+48h: Cevap yoksa 1 kez daha dene
- [ ] **3.5.4** Yorum recovery tracking (kurtarılan/kurtarılamayan)
- [ ] **3.5.5** Kurtarma oranı dashboard'da göster
- [ ] **3.5.6** DB:
  ```sql
  review_alerts (id, tenant_id, provider, external_review_id, rating, review_text, customer_phone, recovery_status, created_at, updated_at)
  ```

---

### GR-3.6: İade Çevirme v2 (Senaryo S3 genişleme)

> **Servis:** `AgentAI` + `Outbound` + `Integrations`
> **Sektör:** E-ticaret

- [ ] **3.6.1** Otomatik kupon oluşturma (tenant tanımlı limitler içinde)
- [ ] **3.6.2** Değişim stok kontrolü (Integrations'tan stok sorgula)
- [ ] **3.6.3** İade çevirme başarı oranı (%): çevrilen / toplam iade
- [ ] **3.6.4** Kurtarılan gelir dashboard'da göster
- [ ] **3.6.5** Follow-up (T+24h: "Değişim ürününüz yolda, memnun musunuz?")

---

### GR-3.7: Multi-Language AI (TR/EN)

> **Servis:** `ChatAnalysis` + `AgentAI` + `Knowledge`
> **Sektör:** Tümü (ağırlıklı sağlık niche hazırlık)

- [ ] **3.7.1** ChatAnalysis'e language detection ekle
- [ ] **3.7.2** AgentAI response'unu tespit edilen dilde döndür
- [ ] **3.7.3** Knowledge base multi-language support (aynı FAQ, farklı diller)
- [ ] **3.7.4** Outbound template'lerde dil seçimi
- [ ] **3.7.5** Desteklenen diller: TR, EN (AR Phase 4-5'te)

**Yapılmayacak:**
- ❌ Arapça (sağlık niche kanıtlanmadan)
- ❌ Otomatik çeviri (ayrı dil şablonları kullan)

---

### GR-3.8: Dashboard Genişletme

> **Servis:** Dashboard
> **Sektör:** Tümü

- [ ] **3.8.1** Knowledge management UI (doc yükle, FAQ ekle)
- [ ] **3.8.2** Intent performance (hangi intent ne kadar çözüyor)
- [ ] **3.8.3** Top unanswered questions (bilgi tabanında eksik ne var)
- [ ] **3.8.4** Müşteri bazlı deflection rate
- [ ] **3.8.5** Outbound campaign dashboard (gönderim/okunma/dönüşüm)
- [ ] **3.8.6** İade çevirme oranı + kurtarılan gelir
- [ ] **3.8.7** Yorum kurtarma oranı + etki

---

## Çıkış Kriterleri (Phase 4'e Geçiş Şartı)

- [ ] E-ticaret: 15+ aktif ödeyen müşteri
- [ ] Diş: 7+ aktif ödeyen klinik
- [ ] Estetik: 5+ aktif ödeyen klinik
- [ ] En az 3 müşteri (her niche'ten 1+) knowledge base'i aktif kullanıyor
- [ ] Deflection rate %50+ (e-ticaret, Knowledge ile)
- [ ] Fiyat→randevu dönüşüm %35+ (diş)
- [ ] Lead→randevu dönüşüm %35+ (estetik)
- [ ] Outbound conversion rate %5+ (mesaj → aksiyon)
- [ ] İade çevirme oranı %15+ (çevrilen / toplam iade)
- [ ] Multi-language çalışıyor (TR + EN)
- [ ] Kurumsal talepler geliyor → "SSO var mı? Audit log var mı?"
