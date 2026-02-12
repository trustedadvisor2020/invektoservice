# Phase 1 — Core Otomasyon (TÜM Sektörler)

> **Hafta:** 3-8 (gerçekçi: 10-15 hafta solo founder)
> **MRR Hedefi:** 200-300K TL
> **Müşteri Hedefi:** 60+ (mevcut + yeni)
> **Bağımlılık:** Phase 0 tamamlanmış olmalı
> **Durum:** 🔄 Devam Ediyor

---

## Durum Takibi

| Alt Gereksinim | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------------------|--------|
| GR-1.1 Chatbot / Flow Builder | 🔄 Devam Ediyor | — | FB-1 ✅, FB-2 ✅, FB-3~5 bekliyor → [flow-builder.md](../flow-builder.md) |
| GR-1.2 AI Agent Assist | ⬜ Başlamadı | — | — |
| GR-1.3 Broadcast / Toplu Mesaj + Trigger | ✅ Tamamlandı | 2026-02-12 | Invekto.Outbound microservice — broadcast + trigger engine |
| GR-1.4 Otomasyon Dashboard | ⬜ Başlamadı | — | — |
| GR-1.5 Diş Kliniği Pipeline | ⬜ Başlamadı | — | — |
| GR-1.6 Basit Randevu Motoru | ⬜ Başlamadı | — | — |
| GR-1.7 Estetik Lead Pipeline | ⬜ Başlamadı | — | — |
| GR-1.8 KVKK Minimum Koruma | ⬜ Başlamadı | — | — |
| GR-1.9 Invekto ↔ InvektoServis Entegrasyonu | ✅ Tamamlandı | 2026-02-08 | JWT auth, webhook receiver, async callback, PostgreSQL, API contracts |
| GR-1.10 Ops Dashboard Log İyileştirmesi | ⬜ Başlamadı | — | Business View + Akıllı Özet Kartları |

> **Güncelleme:** Bir gereksinim tamamlandığında durumu `✅ Tamamlandı` olarak güncelle ve tarihi yaz.
> Devam ediyorsa `🔄 Devam Ediyor`, bloke ise `🚫 Bloke` yaz.

---

## Özet

Mevcut 50+ müşterinin tamamı faydalanacak. #1 satış engeli ("Chatbot/AI yok mu?") ve #1 churn sebebi (otomasyon eksikliği) çözülecek.

**Satış dili:** "Otomasyon, AI ve chatbot artık var — mesajlarınız otomatik cevaplanıyor"

---

## Yeni Mikro Servisler

| Servis | Port | Sorumluluk |
|--------|------|------------|
| `Invekto.Automation` | 7108 | Chatbot, flow engine, trigger sistemi |
| `Invekto.AgentAI` | 7105 | Agent Assist, intent detection, reply suggestion |
| `Invekto.Outbound` | 7107 | Broadcast, toplu mesaj, zamanlama |

---

## Gereksinimler

### GR-1.1: Chatbot / Flow Builder

> **Servis:** `Invekto.Automation` (port 7108) + `Invekto.Backend` (port 5000, proxy + SPA serve)
> **Sektör:** Tümü
> **Detay:** [flow-builder.md](../flow-builder.md)
> **Durum:** 🔄 Devam Ediyor — FB-1 + FB-2 tamamlandı

Visual Flow Builder (n8n benzeri drag-drop) + Graph-based FlowEngine v2.
Mevcut v1 (menü bazlı) korunur, v2 (graph-based) üstüne biner.

#### Sub-Phases (Flow Builder İç Fazları)

| Sub-Phase | Adı | Durum | Scope |
|-----------|-----|-------|-------|
| **FB-1** | SPA Scaffold + Canvas | ✅ Tamamlandı | React Flow + Zustand + 5 node component |
| **FB-2** | API + Backend Entegrasyon | ✅ Tamamlandı | JWT auth, CRUD, proxy, SPA routing, FlowListPage |
| **FB-3** | FlowEngine v2 (Backend Execution) | ⬜ Başlamadı | Graph traversal, v1→v2 migration, orchestrator dispatch |
| **FB-4** | Genişletilmiş Node'lar | ⬜ Başlamadı | 7 yeni node (logic, AI, action, utility) + UI components |
| **FB-5** | iframe + Polish | ⬜ Başlamadı | postMessage bridge, auto-save, test modu, keyboard shortcuts |

#### FB-1: SPA Scaffold + Canvas ✅ TAMAMLANDI

- [x] **1.1.1** SPA projesi oluştur (React 18 + Vite + TailwindCSS + @xyflow/react)
- [x] **1.1.2** Contract v2 TypeScript types (12 node type, NodeData union)
- [x] **1.1.3** Zustand store (nodes, edges, selection, undo/redo max 50)
- [x] **1.1.4** 5 node component: trigger_start, message_text, message_menu, action_handoff, utility_note
- [x] **1.1.5** FlowCanvas: drag-drop, self-connection prevention, custom edge (hover X)
- [x] **1.1.6** NodePalette (kategorili sol sidebar) + NodePropertyPanel (type-specific editors)
- [x] **1.1.7** Toolbar: flow adı/açıklama, undo/redo, save, dirty indicator
- [x] **1.1.8** Build PASS (tsc 0 error, vite build OK — JS 368KB gzip 118KB)

#### FB-2: API + Backend Entegrasyon ✅ TAMAMLANDI

- [x] **1.1.9** SPA fallback route → Backend:5000 `/flow-builder/{**slug}` → index.html
- [x] **1.1.10** JWT prefix → `/api/v1/flow-builder/` JWT koruması
- [x] **1.1.11** FlowBuilderClient.cs → Backend → Automation proxy class
- [x] **1.1.12** Proxy endpoint'ler (GET/PUT flows, validate, activate, migrate-v1)
- [x] **1.1.13** SPA API client (`lib/api.ts` — load/save flow, JWT header)
- [x] **1.1.14** FlowListPage: flow yönetim ekranı (liste, aktif/pasif toggle, sil)
- [x] **1.1.15** Auth: standalone login + iframe postMessage desteği

#### FB-3: FlowEngine v2 (Backend Execution)

- [ ] **1.1.16** FlowGraphV2.cs — in-memory adjacency list, node lookup
- [ ] **1.1.17** FlowEngineV2.cs — node executor + chain traversal (auto-traverse vs wait-point)
- [ ] **1.1.18** FlowValidator.cs — graph validation rules (orphan, cycle, empty text, missing handle)
- [ ] **1.1.19** FlowMigrator.cs — v1 → v2 otomatik conversion
- [ ] **1.1.20** Orchestrator dispatch — version check → v1 veya v2 engine
- [ ] **1.1.21** Error codes (INV-AT-006 ~ INV-AT-010)

#### FB-4: Genişletilmiş Node'lar

- [ ] **1.1.22** Logic: condition (if/else), switch (multi-branch)
- [ ] **1.1.23** AI: intent detection, FAQ arama (mevcut IntentDetector/FaqMatcher reuse)
- [ ] **1.1.24** Action: api_call (webhook/HTTP), delay (bekle N saniye)
- [ ] **1.1.25** Utility: set_variable (session değişken atama)
- [ ] **1.1.26** 7 yeni React Flow node component + property panel editors

#### FB-5: iframe + Polish

- [ ] **1.1.27** iframe bridge (postMessage protocol: init, ready, auth_required, flow_saved)
- [ ] **1.1.28** Auto-detection (`window.self !== window.top` → iframe mode)
- [ ] **1.1.29** Tema desteği (dark/light theme switching)
- [ ] **1.1.30** Auto-save (debounced 5s idle) + keyboard shortcuts (Ctrl+S/Z/Y, Delete)
- [ ] **1.1.31** Flow validation UI (inline hata/uyarı overlay)
- [ ] **1.1.32** Test modu — canlı flow simülasyonu (chat panel + canvas node highlight)

#### Yapılmayacak (Phase 1 Scope Dışı)

- ❌ RAG / Knowledge base (Roadmap Phase 3)
- ❌ Guardrails / PII detection (Roadmap Phase 4)
- ❌ Campaign yönetimi, A/B test (Roadmap Phase 3)

---

### GR-1.2: AI Agent Assist

> **Servis:** `Invekto.AgentAI` (port 7105)
> **Sektör:** Tümü
> **Tahmini süre:** 2-3 hafta

**Yapılacak:**
- [ ] **1.2.1** AgentAI servis iskeletini oluştur (port 7105, health check, tenant izolasyon)
- [ ] **1.2.2** Suggested reply — AI'ın önerdiği cevabı 1 tıkla gönder
  - Mesaj gelince → intent algıla → cevap öner → agent onaylar/düzenler/reddeder
- [ ] **1.2.3** Intent detection + cevap önerisi pipeline
  - Message → Intent → Response generation → Output
- [ ] **1.2.4** Otomatik etiketleme (AI bazlı konu tespiti)
  - Gelen mesajın konusunu algıla → etiket ata
- [ ] **1.2.5** Dinamik şablon değişkenleri
  - `{{isim}}`, `{{firma}}`, `{{siparis_no}}` desteği
  - Template engine

**Yapılmayacak:**
- ❌ Tone presets (Phase 3)
- ❌ "Neden bu cevap" açıklaması (Phase 3)
- ❌ Next Best Action (Phase 5)

---

### GR-1.3: Broadcast / Toplu Mesaj + Trigger

> **Servis:** `Invekto.Outbound` (port 7107)
> **Sektör:** Tümü
> **Tahmini süre:** 2-3 hafta

**Yapılacak:**
- [ ] **1.3.1** Outbound servis iskeletini oluştur (port 7107, health check, tenant izolasyon)
- [ ] **1.3.2** Toplu mesaj gönderimi (segment bazlı)
  - Hedef kitle seçimi (etiket, kanal, tarih filtresi)
  - Gönderim başlatma + kuyruğa alma
- [ ] **1.3.3** Basit trigger engine (event-based otomasyon)
  - Desteklenen event'ler: yeni sohbet, etiket değişimi, sohbet kapatma
  - Event → template eşleştirme
- [ ] **1.3.4** Template engine (değişkenli mesaj şablonları)
  - WhatsApp template approval uyumlu
- [ ] **1.3.5** Gönderim kuyruğu + rate limiting
  - WhatsApp Business API kurallarına %100 uyum
  - Rate limit: tenant bazlı, dakika bazlı
- [ ] **1.3.6** Opt-out yönetimi
  - "STOP" → otomatik unsubscribe
  - Opt-out listesi tenant bazlı
- [ ] **1.3.7** Delivery status tracking
  - Status: queued → sent → delivered → read → failed
  - Failed reason kayıt
- [ ] **1.3.8** DB tabloları oluştur:
  ```sql
  outbound_templates (id, tenant_id, name, trigger_event, message_template, variables_json, is_active, created_at, updated_at)
  outbound_messages (id, tenant_id, template_id, recipient_phone, message_text, status, sent_at, delivered_at, read_at, failed_reason, created_at)
  outbound_optouts (id, tenant_id, phone, reason, created_at)
  ```

**Yapılmayacak:**
- ❌ AI-generated personalization (Phase 3)
- ❌ Campaign yönetimi (Phase 3)
- ❌ A/B testing (Phase 3)
- ❌ Conversion tracking (Phase 3)

---

### GR-1.4: Otomasyon Dashboard

> **Servis:** Mevcut React Dashboard genişler
> **Sektör:** Tümü

**Yapılacak:**
- [ ] **1.4.1** Kaç soru geldi (toplam / günlük)
- [ ] **1.4.2** Kaç tanesi otomatik cevaplandı (deflection rate)
- [ ] **1.4.3** Kaç tanesi temsilciye devredildi
- [ ] **1.4.4** Günlük/haftalık trend grafikleri
- [ ] **1.4.5** DB tablosu:
  ```sql
  daily_metrics (id, tenant_id, date, total_messages, auto_resolved, human_handled, avg_response_time_sec, created_at)
  ```

**Yapılmayacak:**
- ❌ SLA tracker (Phase 4)
- ❌ QA scoring (Phase 6)
- ❌ Revenue attribution (Phase 5)

---

### GR-1.5: Diş Kliniği — Fiyat Sorusu Pipeline

> **Servis:** `ChatAnalysis` :7101 genişleme + Backend :5000
> **Sektör:** Diş

**Yapılacak:**
- [ ] **1.5.1** Intent tanımla: "implant ne kadar" / "fiyat ne" / "tedavi ücreti"
- [ ] **1.5.2** Intent eşleşince → fiyat aralığı + ücretsiz muayene teklifi gönder
- [ ] **1.5.3** Randevu alma intent'i: "randevu almak istiyorum" → slot öner
- [ ] **1.5.4** Eşleşmezse → sekretere devret (human handoff)

**Yapılmayacak:**
- ❌ HBYS entegrasyonu (çok erken)
- ❌ Tedavi planı detayı (doktor verir)
- ❌ Ödeme/depozit sistemi (Phase 3+)

---

### GR-1.6: Diş Kliniği — Basit Randevu Motoru

> **Servis:** Backend :5000 + basit cron hatırlatma
> **Sektör:** Diş

**Yapılacak:**
- [ ] **1.6.1** Haftalık slot tanımı (gün + saat aralıkları)
- [ ] **1.6.2** Randevu al → WhatsApp teyit mesajı gönder
- [ ] **1.6.3** T-48h hatırlatma (cron job veya Outbound Engine ile)
- [ ] **1.6.4** T-2h son hatırlatma
- [ ] **1.6.5** İptal → slot boşalt
- [ ] **1.6.6** Basit diş dashboard'u:
  - Kaç fiyat sorusu geldi
  - Kaç tanesi randevuya döndü (dönüşüm oranı)
  - No-show sayısı + oranı
  - Haftalık trend

**Yapılmayacak:**
- ❌ Google Calendar sync (Phase 2)
- ❌ Bekleme listesi (Phase 2)
- ❌ Doktor bazlı slot (Phase 2)
- ❌ Online ödeme (Phase 3+)

---

### GR-1.7: Estetik Klinik — Lead Pipeline

> **Servis:** `ChatAnalysis` genişleme + Backend + Dashboard
> **Sektör:** Estetik

**Yapılacak:**
- [ ] **1.7.1** Intent tanımla: "fiyat ne kadar" / "botox" / "dolgu" / "randevu"
- [ ] **1.7.2** Fiyat sorusuna → kişiselleştirilmiş aralık + konsültasyon teklifi
- [ ] **1.7.3** Before/after fotoğraf talebi → hazır galeri linki
- [ ] **1.7.4** Eşleşmezse → operasyon sorumlusuna devret
- [ ] **1.7.5** Basit lead tracking:
  - Lead kaydı (isim, telefon, ilgi alanı, kaynak)
  - Lead durumu (yeni → iletişim → randevu → hasta)
  - Basit follow-up hatırlatma (T+24h cevap yoksa tekrar mesaj)
- [ ] **1.7.6** Estetik dashboard'u:
  - Kaç lead geldi (kaynak bazlı)
  - Lead → randevu dönüşüm oranı
  - Yanıt süresi
  - Haftalık trend

**Yapılmayacak:**
- ❌ Instagram API entegrasyonu (manuel DM→WA yeterli)
- ❌ Otomatik lead scoring (Phase 2)
- ❌ Ödeme/depozit (Phase 3+)

---

### GR-1.8: KVKK Minimum Koruma (Sağlık Niche)

> **Servis:** Tüm servisler
> **Sektör:** Sağlık (Diş + Estetik)

- [ ] **1.8.1** Disclaimer: AI sağlık tavsiyesi vermez, her otomasyon mesajında disclaimer ekle
- [ ] **1.8.2** Açık rıza: WhatsApp otomasyon başlamadan hasta onayı (opt-in mesajı)
- [ ] **1.8.3** Veri minimizasyonu: Sadece isim, telefon, randevu — tıbbi kayıt/rapor saklanmaz
- [ ] **1.8.4** Erişim kontrolü: Hasta verisine sadece ilgili tenant erişir (mevcut multi-tenant yeterli)
- [ ] **1.8.5** Fotoğraf politikası: Hasta fotoğrafı Invekto'ya yüklenmez (Phase 4'e kadar)

---

### GR-1.9: Invekto ↔ InvektoServis Entegrasyonu

> **Servis:** Backend :5000 ↔ Ana Uygulama (.NET)
> **Tahmini süre:** 2-3 hafta

- [x] **1.9.1** API contract tanımla → Webhook push + async callback (arch/contracts/)
- [x] **1.9.2** Auth token validation → JWT HMAC-SHA256 middleware (Invekto.Shared/Auth/)
- [x] **1.9.3** Tenant ID eşleştirme → PostgreSQL tenant_registry + TenantContext DTO
- [x] **1.9.4** Error handling + retry mekanizması → 3x exponential backoff callback
- [x] **1.9.5** Latency monitoring → X-Processing-Time-Ms header + 200ms threshold logging

---

### GR-1.10: Ops Dashboard Log İyileştirmesi

> **Servis:** Backend :5000 (Shared + Dashboard)
> **Tahmini süre:** 1 hafta

**Yapılacak:**
- [ ] **1.10.1** LogEntry'ye `category` alanı ekle: `api` | `system` | `health` | `step`
  - LogRequest → `api`, LogSystem → `system`, LogStep → `step`
  - Health/ready/ops istekleri → loglama skip veya `health` kategorisi
- [ ] **1.10.2** LogReader'a category filtresi ekle
  - Grouped query default: sadece `api` + `step` (gürültü gizli)
  - `?category=all` ile tüm loglar görülebilir
- [ ] **1.10.3** Dashboard Business View (default)
  - Sadece iş mantığı eventleri göster (API çağrıları + sonuçlar)
  - Toggle: "Business" / "All"
  - Health check, ops, system logları gizli
- [ ] **1.10.4** Akıllı Özet Kartları
  - Her operasyon tek satır: `ChatAnalysis → analyze → 9 mesaj → OK (269ms)`
  - Hata operasyonları: kırmızı badge + hata mesajı
  - Tıklayınca mevcut timeline detayı açılır
- [ ] **1.10.5** Log entry'lere iş özeti alanı ekle (`summary` field)
  - API endpoint handler'lar özet bilgiyi loga yazar
  - Örnek: "9 mesaj analiz edildi", "Webhook event alındı (tenant: 42)"

**Yapılmayacak:**
- ❌ Log aggregation / external service (ELK, Grafana vb.)
- ❌ Real-time WebSocket streaming (polling yeterli)
- ❌ Log export (Phase 2+)

---

## User First-Value Flow

```
1. Otomasyon modülünü aktifleştir
   └── Chatbot ayarla → FAQ'ları gir → ✓ Aktif

2. "Otomatik Cevaplama" toggle'ını AÇ

3. İlk otomatik cevap gönderildi
   └── ⚡ AHA MOMENT: "Gerçekten otomatik cevapladı!"

4. Dashboard'da ilk sonuç
   └── "Bugün X mesaj otomatik cevaplandı"
   └── "Y dakika tasarruf edildi"

Day 7:  Haftalık rapor → "%30 mesaj otomatik çözüldü"
Day 30: "Bu ay 450 mesaj otomatik, 1 temsilci tasarruf"
```

---

## AI Güven Eğrisi (Trust Ladder)

| Dönem | Agent Davranışı | Sistem Davranışı |
|-------|----------------|------------------|
| Hafta 1 | AI önerisini okuyor, kendi yazıyor | Sadece öneri (asla otomatik gönderme) |
| Hafta 2 | AI önerisini kabul etmeye başlıyor | Kabul oranı ölçülüyor |
| Hafta 3-4 | AI'ya güveniyor, bazı soruları bırakıyor | "Otomatik cevapla" özelliği açılıyor |
| Ay 2+ | Supervisory role'e geçiyor | AI çoğunu çözer, agent kontrol eder |

---

## Onboarding (İlk 48 Saat)

```
İLK 30 DAKİKA:
  1. Mevcut Invekto hesabına otomasyon modülü aktifleştir
  2. Chatbot konfigürasyonu (sık sorulan sorular + cevaplar)
  3. Broadcast listesi oluştur (mevcut müşteri segmenti)
  4. "Otomatik Cevaplama" toggle'ını AÇ

İLK 24 SAAT:
  5. İlk otomatik cevap → ⚡ AHA MOMENT
  6. Dashboard'da ilk metrik

İLK 48 SAAT:
  7. İlk mini-rapor email'i
  8. Onboarding call (15dk) — feedback + ayar ince tuning
```

---

## Core SaaS Metrikleri (Zorunlu Ölçüm)

| Metrik | Tanım | Hedef |
|--------|-------|-------|
| TTFAR | Time to First Automated Reply | < 24h |
| Weekly Deflection % | Otomatik çözülen / toplam mesaj | %30+ |
| 30-Day Logo Retention | 30 gün sonra ödeyen müşteri oranı | %80+ |
| Activation | En az 1 otomatik cevap + dashboard ziyareti | İlk 24h |
| Net Logo Churn | Aylık müşteri kaybı | < %10/ay |

---

## Çıkış Kriterleri (Phase 2'ye Geçiş Şartı)

- [ ] Deflection rate %30+ (otomatik cevaplanan / toplam)
- [ ] Otomasyon kullanan müşteri sayısı 20+ (mevcut tabanın %40+)
- [ ] Time to first automated reply < 24h
- [ ] Müşteriler "sektörüme özel özellik olsa" diyor → Phase 2 scope'u netleşiyor

### Niche Bazlı Başarı Kriterleri

| Kriter | E-ticaret | Diş | Estetik |
|--------|-----------|-----|---------|
| Aktif müşteri | 1 satıcı | 1 klinik | 1 klinik |
| AHA moment | Kargo sorusu oto-cevap | Fiyat→randevu dönüşümü | Lead'e hızlı cevap |
| Deflection rate | %30+ | N/A | N/A |
| Dönüşüm oranı | N/A | Fiyat→randevu %20+ | Lead→randevu %25+ |
| No-show önleme | N/A | %25→%10 altı | N/A |
| 2. ay ödeme | Evet | Evet | Evet |
