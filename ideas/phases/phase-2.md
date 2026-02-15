# Phase 2 — AI Derinleştirme + Kritik Niche (Hibrit)

> **Hafta:** 9-16
> **MRR Hedefi:** 300-500K TL
> **Müşteri Hedefi:** 75+
> **Bağımlılık:** Phase 1 tamamlanmış olmalı
> **Durum:** ⬜ Başlamadı
>
> **v4.1 Hibrit Yapılanma (2026-02-15):** Eski Phase 2 (Niche) ve Phase 3 (AI) karıştırılarak
> yeniden yapılandırıldı. RAG/Knowledge tüm sektörlere erken fayda sağlar, randevu motoru
> mevcut klinik müşterilerini memnun eder, dashboard metrik ölçümünü başlatır.
>
> **v4.2 Optimizasyon (2026-02-15):** Randevu motoru core/advanced bölündü (advanced → Phase 3 GR-3.19).
> Dashboard'a metadata logging + basit FRT eklendi (Phase 6 Mining/SLA hazırlığı).
>
> **v4.6 WA Analytics Entegrasyonu (2026-02-14):** GR-2.1 Knowledge Service, WA-3 (Training Data Export) ile
> BERABER yapilacak. WA-2 NLP Pipeline ciktilari (FAQ clusters, intent, sentiment) direkt Knowledge DB'ye akar.
> Isimlendirme: **WA** = WhatsApp Analytics fazlari, **RP** = Roadmap Phase. Bkz: `arch/active-work.md` Execution Queue.

---

## Durum Takibi

| Alt Gereksinim | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------------------|--------|
| GR-2.1 Knowledge Service (RAG) | ✅ Tamamlandı (A+B) | 2026-02-15 | Phase A: core + import + retrieval. Phase B: PDF + chunking + UI. Commit: 385d3e0, 89bbe72 |
| GR-2.2 Agent Assist v2 (RAG beslemeli) | ⬜ Başlamadı | — | ← eski GR-3.3. **PKT-1** |
| GR-2.3 Multi-Language AI (TR/EN) | ⬜ Başlamadı | — | ← eski GR-3.7 + GR-2.16. **PKT-1** |
| GR-2.4 Randevu Motoru (basit→v2) | ⬜ Başlamadı | — | ← eski GR-2.10. **PKT-2** |
| GR-2.5 Otomasyon Dashboard + Log | ⬜ Başlamadı | — | ← eski GR-2.17 + GR-1.10.4/5. **PKT-3** |
| GR-2.6 KVKK Minimum Koruma | ⬜ Başlamadı | — | ← eski GR-2.18. **PKT-2** |

> **Güncelleme:** Bir gereksinim tamamlandığında durumu `✅ Tamamlandı` olarak güncelle ve tarihi yaz.
> Devam ediyorsa `🔄 Devam Ediyor`, bloke ise `🚫 Bloke` yaz.

---

## Özet

"AI artık şirket verisinden cevap veriyor, sallama yapmıyor." Knowledge base + RAG ile AI doğruluğu artar, agent iş yükü ciddi düşer. Aynı zamanda mevcut klinik müşterilerine randevu motoru ve KVKK koruması sağlanır.

**Satış dili:** "Kendi verinizle cevap veriyor — ürün/tedavi/fiyat sorularını otomatik çözüyor"

**Neden hibrit?**
- RAG **tüm sektörlere** birden fayda sağlar (18 GR yerine 6 GR ile 3x etki)
- Randevu motoru mevcut klinik müşterilerini mutlu eder
- Dashboard ile neyin işe yaradığını ölçmeye başlarız
- KVKK sağlık niche'i için zorunlu

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

### GR-2.1: Knowledge Service (RAG)

> **Servis:** `Invekto.Knowledge` (port 7104) — YENİ
> **Sektör:** Tümü
> **Kaynak:** eski GR-3.1

- [x] **2.1.1** Knowledge servis iskeletini oluştur (port 7104, health check, tenant izolasyon) ✅ Phase A
- [x] **2.1.2** PDF upload + chunking — ✅ Phase B (PdfPig, 512-token/50-overlap)
  - Ürün kataloğu, SSS dokümanları, politika belgeleri
  - Chunk boyutu + overlap stratejisi
- [x] **2.1.3** FAQ editor (hızlı soru-cevap girişi — UI) — ✅ Phase B (Dashboard FaqManager)
- [x] **2.1.4** Embeddings pipeline (pgvector) ✅ Phase A
  - Embedding model: text-embedding-3-large (3072 dim)
  - Tenant bazlı izolasyon (aynı pgvector instance, farklı tenant_id)
- [x] **2.1.5** Retrieval API (topK + tenant izolasyonu) ✅ Phase A
  - Soru → semantic search (pgvector) → keyword fallback (FTS) → context oluştur
- [x] **2.1.6** Kaynak referanslı cevap ("pricing.pdf sayfa 3'e göre...") — ✅ Phase B (sourceType discriminator)
- [x] **2.1.7** Knowledge management UI (Dashboard'da doc yükle, FAQ ekle) — ✅ Phase B (DocumentUpload, FaqManager)
- [x] **2.1.8** DB: ✅ Phase A (8 tablo)
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
- ❌ Knowledge gap report (veri birikmesi lazım — Phase 6)

---

### GR-2.2: Agent Assist v2 (RAG Beslemeli)

> **Servis:** `Invekto.AgentAI` (port 7105) — genişleme
> **Sektör:** Tümü
> **Kaynak:** eski GR-3.3

- [ ] **2.2.1** Reply generation artık Knowledge'dan beslenecek
- [ ] **2.2.2** "Neden bu cevap" açıklaması + kaynak referansı
- [ ] **2.2.3** Tone presets (formal / kısa / samimi)
- [ ] **2.2.4** Multi-turn: AI takip sorusu sorabiliyor
- [ ] **2.2.5** Pipeline kurulumu: message → intent → knowledge lookup → response → output
- [ ] **2.2.6** Kaynak yoksa "insana devret" kuralı

---

### GR-2.3: Multi-Language AI (TR/EN)

> **Servis:** `ChatAnalysis` + `AgentAI` + `Knowledge`
> **Sektör:** Tümü (ağırlıklı sağlık niche)
> **Kaynak:** eski GR-3.7 + GR-2.16 birleştirildi

- [ ] **2.3.1** ChatAnalysis'e language detection ekle
- [ ] **2.3.2** AgentAI response'unu tespit edilen dilde döndür
- [ ] **2.3.3** Knowledge base multi-language support (aynı FAQ, farklı diller)
- [ ] **2.3.4** Outbound template'lerde dil seçimi
- [ ] **2.3.5** İngilizce template seti (fiyat, randevu, bilgi)
- [ ] **2.3.6** Yabancı hasta flag (dashboard'da "yabancı hasta" etiketi)
- [ ] **2.3.7** Desteklenen diller: TR, EN (AR Phase 5'te)

**Yapılmayacak:**
- ❌ Arapça (sağlık niche kanıtlanmadan)
- ❌ Otomatik çeviri (ayrı dil şablonları kullan)

---

### GR-2.4: Randevu Motoru (Core)

> **Servis:** Backend genişleme + `Invekto.Outbound` entegrasyonu
> **Sektör:** Sağlık (Diş + Estetik)
> **Kaynak:** eski GR-2.10 (+ GR-1.6 ile birleşmiş)
>
> **v4.2:** Advanced özellikler (Google Calendar, doktor bazlı slot, bekleme listesi, no-show prediction,
> fiyat editor) Phase 3 GR-3.19'a taşındı. Bu GR sadece core randevu akışını kurar.

- [ ] **2.4.1** Basit haftalık slot tanımı (gün + saat aralıkları)
- [ ] **2.4.2** Randevu al → WhatsApp teyit mesajı gönder
- [ ] **2.4.3** T-48h / T-2h hatırlatma (Outbound Engine ile)
- [ ] **2.4.4** İptal → slot boşalt
- [ ] **2.4.5** Self-service slot tanımı (Dashboard'dan)
- [ ] **2.4.6** DB:
  ```sql
  appointments (id, tenant_id, patient_phone, patient_name, doctor_id, service_type, slot_start, slot_end, status, reminder_sent_48h, reminder_sent_2h, confirmed, no_show, no_show_count, created_at, updated_at)
  appointment_slots (id, tenant_id, doctor_id, day_of_week, start_time, end_time, max_capacity, is_active, created_at)
  ```

**Phase 3'e taşınan (v4.2 → GR-3.19):**
- ➡️ Google Calendar sync (2-way)
- ➡️ Doktor bazlı slot yönetimi (specialist vs genel)
- ➡️ Bekleme listesi (iptal olursa → sıradaki hastaya sor)
- ➡️ No-show prediction (2+ kez no-show → extra hatırlatma)
- ➡️ Fiyat aralığı editor (tedavi → min/max TL)

---

### GR-2.5: Otomasyon Dashboard + Log İyileştirme

> **Servis:** Dashboard (React) + Backend genişleme
> **Sektör:** Tümü
> **Kaynak:** eski GR-2.17 + GR-1.10.4/5

- [ ] **2.5.1** Deflection rate: Kaç mesaj otomatik cevaplandı / toplam
- [ ] **2.5.2** Handoff rate: Kaç tanesi temsilciye devredildi
- [ ] **2.5.3** Günlük/haftalık trend grafikleri
- [ ] **2.5.4** Akıllı Özet Kartları (log stream'de operasyon özeti)
- [ ] **2.5.5** Log entry'lere `summary` field ekle
- [ ] **2.5.6** Intent performance (hangi intent ne kadar çözüyor)
- [ ] **2.5.7** Top unanswered questions (bilgi tabanında eksik ne var)
- [ ] **2.5.8** Müşteri bazlı deflection rate
- [ ] **2.5.9** Basit FRT (First Response Time) ölçümü — ortalama ilk yanıt süresi (Phase 6 SLA hazırlığı)
- [ ] **2.5.10** Conversation metadata log başlat (süre, intent, resolution, sentiment — Phase 6 Mining için veri birikimi)
- [ ] **2.5.11** DB:
  ```sql
  daily_metrics (id, tenant_id, date, total_messages, auto_resolved, human_handled, avg_response_time_sec, created_at)
  conversation_metadata (id, tenant_id, conversation_id, duration_sec, primary_intent, resolution_type, sentiment_score, agent_id, created_at)
  ```

**Yapılmayacak:**
- ❌ Tam SLA tracker (Phase 6 — basit FRT burada başlar)
- ❌ QA scoring (Phase 6)
- ❌ Revenue attribution (Phase 5)
- ❌ Script compliance check (Phase 4)

---

### GR-2.6: KVKK Minimum Koruma (Sağlık Niche)

> **Servis:** Tüm servisler
> **Sektör:** Sağlık (Diş + Estetik)
> **Kaynak:** eski GR-2.18 (+ GR-1.8)

- [ ] **2.6.1** Disclaimer: AI sağlık tavsiyesi vermez, her otomasyon mesajında disclaimer ekle
- [ ] **2.6.2** Açık rıza: WhatsApp otomasyon başlamadan hasta onayı (opt-in mesajı)
- [ ] **2.6.3** Veri minimizasyonu: Sadece isim, telefon, randevu — tıbbi kayıt/rapor saklanmaz
- [ ] **2.6.4** Erişim kontrolü: Hasta verisine sadece ilgili tenant erişir (mevcut multi-tenant yeterli)
- [ ] **2.6.5** Fotoğraf politikası: Hasta fotoğrafı Invekto'ya yüklenmez (Phase 4'e kadar)

---

## Çıkış Kriterleri (Phase 3'e Geçiş Şartı)

- [ ] Knowledge base çalışıyor, en az 3 tenant aktif kullanıyor
- [ ] AI cevapları Knowledge'dan besleniyor (RAG çalışıyor)
- [ ] Deflection rate %30+ (dashboard'da ölçülüyor)
- [ ] Multi-language çalışıyor (TR + EN)
- [ ] Randevu motoru aktif, en az 2 klinik kullanıyor
- [ ] No-show hatırlatma gönderiliyor
- [ ] KVKK disclaimer aktif (sağlık niche müşterilerde)
- [ ] Otomasyon dashboard metrikleri görünüyor

### Niche Bazlı Başarı Kriterleri

| Kriter | Tüm Sektörler | Sağlık |
|--------|---------------|--------|
| Knowledge base kullanımı | 3+ tenant | 2+ klinik |
| Deflection rate | %30+ | N/A |
| Multi-language | TR + EN aktif | EN aktif (yabancı hasta) |
| Randevu | N/A | 2+ klinik, no-show <%15 |
| KVKK | N/A | Disclaimer aktif |

---

## Notlar

- Bu phase eski Phase 2 ve Phase 3'ün hibrit birleşimidir (v4.1, 2026-02-15)
- RAG tüm sektörlere fayda sağlar — niche-özel intent genişleme Phase 3'e taşındı
- Randevu motoru mevcut klinik müşterilerinin en acil talebi
- Trendyol/HB API, e-ticaret niche-özel işler, lead pipeline Phase 3'e taşındı
