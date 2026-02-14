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
| GR-1.1 Chatbot / Flow Builder | ✅ Tamamlandı | 2026-02-15 | FB-1~FB-4 ✅, FB-5 core ✅ (test, validation, ghost path, shortcuts). Kalan polish (iframe, auto-save, tema) ertelendi → backlog. [flow-builder.md](../flow-builder.md) |
| GR-1.2 AI Agent Assist | ✅ Tamamlandı | 2026-02-11 | Core tamamlandi: suggest reply + intent + feedback learning + template. Otomatik etiketleme → Phase 2'ye tasindi. |
| GR-1.3 Broadcast / Toplu Mesaj + Trigger | ✅ Tamamlandı | 2026-02-12 | Invekto.Outbound microservice — broadcast + trigger engine |
| ~~GR-1.4 Otomasyon Dashboard~~ | ➡️ Phase 2 | — | Phase 2'ye tasindi (GR-2.17) — niche metrikleri ile birlestirildi |
| ~~GR-1.5 Diş Kliniği Pipeline~~ | ➡️ Phase 2 | — | Phase 2'ye tasindi — GR-2.9 ile birlesti |
| ~~GR-1.6 Basit Randevu Motoru~~ | ➡️ Phase 2 | — | Phase 2'ye tasindi — GR-2.10 ile birlesti |
| ~~GR-1.7 Estetik Lead Pipeline~~ | ➡️ Phase 2 | — | Phase 2'ye tasindi — GR-2.13/2.14 ile birlesti |
| ~~GR-1.8 KVKK Minimum Koruma~~ | ➡️ Phase 2 | — | Phase 2'ye tasindi (GR-2.18) |
| GR-1.9 Invekto ↔ InvektoServis Entegrasyonu | ✅ Tamamlandı | 2026-02-08 | JWT auth, webhook receiver, async callback, PostgreSQL, API contracts |
| GR-1.10 Ops Dashboard Log İyileştirmesi | ✅ Tamamlandı | 2026-02-14 | category ✅, filtre ✅, Business/All toggle ✅. Kalan (Özet Kartları + summary field) → Phase 2'ye tasindi. |

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
| **FB-3** | FlowEngine v2 (Backend Execution) | ✅ Tamamlandı | FlowGraphV2 + FlowEngineV2 + FlowValidator + FlowMigrator + Orchestrator v1/v2 dispatch |
| **FB-4** | Genişletilmiş Node'lar | ✅ Tamamlandı | 7 yeni node handler + 7 SPA component (logic, AI, action, utility) + SSRF koruması |
| **FB-5** | iframe + Polish | ✅ Core Tamamlandı | ✅ test modu, keyboard shortcuts, validation UI, ghost path, flow summary. Ertelendi: iframe bridge, auto-save, tema (backlog) |

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
- [x] **1.1.15** Auth: standalone login (API key → JWT). NOT: iframe postMessage desteği henüz uygulanmadı — bkz FB-5.

#### FB-3: FlowEngine v2 (Backend Execution) ✅ TAMAMLANDI

- [x] **1.1.16** FlowGraphV2.cs — immutable adjacency list, O(1) node/edge lookup (298 satır)
- [x] **1.1.17** FlowEngineV2.cs — pure graph executor, auto-chain + wait-for-input + terminal (291 satır)
- [x] **1.1.18** FlowValidator.cs — 12 validation rule (orphan, dead-end, required fields, edge consistency, cycle detection) (353 satır)
- [x] **1.1.19** FlowMigrator.cs — v1 → v2 otomatik conversion + auto-layout + warnings (258 satır)
- [x] **1.1.20** Orchestrator dispatch — version check → v1 veya v2 engine (AutomationOrchestrator.cs)
- [x] **1.1.21** Error codes (INV-AT-001 ~ INV-AT-021, doküman hedefinin ötesinde genişledi)

#### FB-4: Genişletilmiş Node'lar ✅ TAMAMLANDI

- [x] **1.1.22** Logic: LogicConditionHandler (7 operator, if/else) + LogicSwitchHandler (multi-branch, N+1 handle)
- [x] **1.1.23** AI: AiIntentHandler (Claude Haiku, high/low confidence) + AiFaqHandler (keyword match + DB query)
- [x] **1.1.24** Action: ApiCallHandler (webhook/HTTP + SSRF koruması) + ActionDelayHandler (N saniye bekleme)
- [x] **1.1.25** Utility: SetVariableHandler (session değişken atama, ExpressionEvaluator)
- [x] **1.1.26** 7 yeni React Flow node component + property panel editors + SPA'da 12 node tipi tam

#### FB-5: iframe + Polish ✅ CORE TAMAMLANDI (polish ertelendi)

- [ ] ~~**1.1.27** iframe bridge (postMessage protocol)~~ — ➡️ **Ertelendi** (backlog — standalone login yeterli)
- [ ] ~~**1.1.28** Auto-detection (iframe mode)~~ — ➡️ **Ertelendi** (backlog)
- [ ] ~~**1.1.29** Tema desteği (dark/light)~~ — ➡️ **Ertelendi** (backlog — kozmetik)
- [ ] ~~**1.1.30** Auto-save~~ — ➡️ **Ertelendi** (backlog — Ctrl+S yeterli)
- [x] **1.1.30b** Keyboard shortcuts (Ctrl+S/Z/Y, Delete) ✅
- [x] **1.1.31** Flow validation UI — inline hata/uyarı overlay (red/orange rings + tooltip) ✅
- [x] **1.1.32** Test modu — SimulationPanel + SimulationEngine + chat UI + node highlight + variable inspector ✅

**Dokümanda olmayan ama yapılan ek özellikler:**
- [x] Ghost Path Visualization — erişilemeyen node'ları soluklaştırma (path-enumerator.ts)
- [x] FlowSummaryBar — canlı DFS flow preview (flow-summarizer.ts, 274 satır)
- [x] Simulation Store — Zustand session lifecycle, mock FAQ/intent (simulation-store.ts)
- [x] Deploy script SPA build — dev-to-invekto-services.bat'a FlowBuilder npm ci + build step eklendi

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
- [x] **1.2.1** AgentAI servis iskeleti (port 7105, health check, JWT auth, tenant izolasyon) ✅
- [x] **1.2.2** Suggested reply — Claude Haiku entegrasyonu, ReplyGenerator.cs ✅
  - Mesaj gelince → intent algıla → cevap öner → agent onaylar/düzenler/reddeder
  - **Ek:** AgentProfileBuilder — feedback geçmişinden kişiselleştirilmiş profil oluşturma
- [x] **1.2.3** Intent detection + cevap önerisi pipeline ✅
  - Message → Intent → Response generation → Output (Claude API JSON çıktısı)
- [ ] ~~**1.2.4** Otomatik etiketleme (AI bazlı konu tespiti)~~ — ➡️ **Phase 2'ye taşındı**
- [x] **1.2.5** Dinamik şablon değişkenleri ✅
  - `{{isim}}`, `{{firma}}`, `{{siparis_no}}` desteği
  - TemplateEngine.cs — `{{variable}}` substitution + HTML sanitization

**Dokümanda olmayan ama yapılan ek özellikler:**
- [x] Feedback learning: agent accepted/edited/rejected tracking → suggest_reply_log DB tablosu
- [x] Per-agent profiling: son 20 feedback'ten otomatik profil → Claude prompt'a enjekte
- [x] Backend proxy: Main App → Backend:5000 → AgentAI:7105 (15s timeout, graceful degradation)

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
- [x] **1.3.1** Outbound servis iskeleti (port 7107, health check, JWT auth, tenant izolasyon) ✅
- [x] **1.3.2** Toplu mesaj gönderimi — BroadcastOrchestrator (max 1000 recipient, async queue) ✅
  - Hedef kitle seçimi (etiket, kanal, tarih filtresi)
  - Gönderim başlatma + kuyruğa alma
- [x] **1.3.3** Basit trigger engine — TriggerProcessor ✅
  - Desteklenen event'ler: manual, new_lead, payment_received, appointment_reminder
  - Event → template eşleştirme + opt-out kontrolü
- [x] **1.3.4** Template engine — TemplateEngine.cs (`{{variable}}` substitution + missing var detection) ✅
  - WhatsApp template approval uyumlu
- [x] **1.3.5** Gönderim kuyruğu + rate limiting — RateLimiter (sliding window, 30 msg/min/tenant) ✅
  - Background IHostedService message sender (batch dequeue, FOR UPDATE SKIP LOCKED)
  - Rate limit: tenant bazlı, dakika bazlı
- [x] **1.3.6** Opt-out yönetimi — OptOutManager (STOP/DUR/İPTAL/IPTAL/DURDU/ÇIKIŞ/CIKIS) ✅
  - Otomatik keyword detection + batch opt-out checking
  - Opt-out listesi tenant bazlı
- [x] **1.3.7** Delivery status tracking ✅
  - Status: queued → sending → sent → delivered → read → failed
  - Failed reason kayıt + external_message_id tracking
- [x] **1.3.8** DB tabloları ✅ (4 tablo — dokümandaki 3 + outbound_broadcasts eklendi):
  ```sql
  outbound_templates (id, tenant_id, name, trigger_event, message_template, variables_json, is_active, created_at, updated_at)
  outbound_broadcasts (id UUID, tenant_id, template_id, status, total_recipients, queued, sent, delivered, read, failed, scheduled_at, created_at, started_at, completed_at)
  outbound_messages (id, tenant_id, broadcast_id, template_id, recipient_phone, message_text, status, external_message_id, sent_at, delivered_at, read_at, failed_reason, created_at)
  outbound_optouts (id, tenant_id, phone, reason, created_at)
  ```

**Yapılmayacak:**
- ❌ AI-generated personalization (Phase 3)
- ❌ Campaign yönetimi (Phase 3)
- ❌ A/B testing (Phase 3)
- ❌ Conversion tracking (Phase 3)

---

### ~~GR-1.4 ~ GR-1.8: Phase 2'ye Taşındı~~

> **Tarih:** 2026-02-15
> **Sebep:** Core otomasyon (chatbot + AI + broadcast) tamamlandı. Niche-özel işler (dashboard, diş pipeline, randevu motoru, estetik lead, KVKK) doğal olarak Phase 2 scope'una ait — niche güçlendirme ile birleştirildi.

| Eski GR | Yeni Yer | Açıklama |
|---------|----------|----------|
| GR-1.4 Otomasyon Dashboard | **GR-2.17** (yeni) | Deflection rate, trend grafikleri, daily_metrics |
| GR-1.5 Diş Pipeline | **GR-2.9** ile birleşti | Fiyat→randevu intent, diş dashboard |
| GR-1.6 Randevu Motoru | **GR-2.10** ile birleşti | Basit slot + hatırlatma → v2'nin parçası |
| GR-1.7 Estetik Lead | **GR-2.13/2.14** ile birleşti | Lead tracking, estetik dashboard |
| GR-1.8 KVKK | **GR-2.18** (yeni) | Disclaimer, opt-in, veri minimizasyonu |

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
- [x] **1.10.1** LogEntry'ye `category` alanı eklendi ✅ (LogEntry.cs:62)
  - LogRequest → `api`, LogSystem → `system`, LogStep → `step`
  - JsonLinesLogger.cs: category bazlı loglama (satır 53, 111)
- [x] **1.10.2** LogReader'a category filtresi eklendi ✅ (LogReader.cs:137, 169)
  - Grouped query default: sadece `api` + `step` (gürültü gizli)
  - `?category=all` ile tüm loglar görülebilir
- [x] **1.10.3** Dashboard Business View ✅ (LogStream.tsx:12, 121, 156)
  - Sadece iş mantığı eventleri göster (API çağrıları + sonuçlar)
  - Toggle: "Business" / "All"
  - Health check, ops, system logları gizli
- [ ] ~~**1.10.4** Akıllı Özet Kartları~~ — ➡️ **Phase 2'ye taşındı** (GR-2.17 Dashboard ile birleştirildi)
- [ ] ~~**1.10.5** Log entry'lere iş özeti alanı ekle (`summary` field)~~ — ➡️ **Phase 2'ye taşındı**

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

> **Güncelleme (2026-02-15):** Niche-özel kriterler Phase 2'ye taşındı. Phase 1 çıkış kriterleri core otomasyon altyapısına odaklanır.

- [x] Core otomasyon servisleri çalışıyor (Automation:7108, AgentAI:7105, Outbound:7107) ✅
- [x] Flow Builder v2 functional (12 node, visual editor, test modu, validation) ✅
- [x] Integration bridge çalışıyor (JWT, webhook, callback) ✅
- [x] Deploy pipeline çalışıyor (FTPES, NSSM services) ✅
- [x] FB-5 core tamamlandı (test, validation, ghost path, shortcuts) ✅ — polish (iframe, auto-save, tema) ertelendi
- [ ] En az 1 tenant production'da v2 chatbot kullanıyor (Q operational task)

> **Not:** FB-5 polish items (iframe bridge, auto-save, tema) ertelendi (2026-02-15).
> Standalone login çalışıyor, Ctrl+S mevcut. Bu items ihtiyaç olduğunda backlog'dan çekilir.
