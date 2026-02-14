# Flow Builder Roadmap

> Tum Flow Builder fazlarinin ozeti. Her fazin detayli plan JSON'u `arch/plans/` altinda.
>
> **DIKKAT:** Bu dosyadaki "FB-X" numaralari Flow Builder'in **ic fazlaridir**.
> Ana roadmap fazlari (Phase 0-7) icin bkz: `ideas/phases/`. Tum FB fazlari **Roadmap Phase 1** (GR-1.1) kapsamindadir.

## Genel Bakis

| Faz | Baslik | Durum | Risk | Plan JSON | AHA |
|-----|--------|-------|------|-----------|-----|
| FB-1 | SPA UI & Canvas | DONE | LOW | (yok - FB-2 oncesi) | - |
| FB-2 | Multi-flow + API + Auth | DONE | HIGH | `20260212-flow-builder-phase2.json` | - |
| FB-2.5 | SPA Quick Wins (AHA) | **DONE** | LOW | `20260213-flow-builder-phase25.json` | #1 #2 #6 |
| FB-3a | FlowEngine v2 + Validator + Migrator | **DONE** | HIGH | `20260213-flow-builder-phase3.json` | - |
| FB-3b | Test Simulasyon + Tek Tikla Test | **DONE** | HIGH | (3a ile ayni plan) | #4 |
| FB-3c | Validation UI + Insights + Polish | **DONE** | MEDIUM | (3a ile ayni plan) | #3 #5 |
| FB-4a | Logic + Delay + SetVariable Node'lari | **DONE** | LOW | `20260214-flow-builder-phase4a.json` | - |
| FB-4b | AI + API Call Node'lari | **DONE** | MEDIUM | `20260214-flow-builder-phase4b.json` | - |
| FB-5a | iframe Embed (Main App entegrasyon) | **NEXT** | HIGH | (henuz yok) | - |
| FB-5b | Trafik Heatmap (analytics) | PLANNED | MEDIUM | (henuz yok) | #7 |
| FB-5c | UX Polish (auto-save, dark mode, export) | PLANNED | LOW | (henuz yok) | - |
| FB-5d | Deploy script SPA build step | **DONE** | LOW | — | - |

---

## FB-1: SPA UI & Canvas (DONE)

**Tamamlanma:** 2026-02-12

- React 18 + TypeScript + Vite + TailwindCSS + React Flow (xyflow) + Zustand
- n8n-style gorsel drag-drop chatbot flow editor
- 5 node tipi UI: trigger_start, message_text, message_menu, action_handoff, utility_note
- Property panel (sag taraf), node palette (sol taraf)
- Undo/redo (50 snapshot), edge silme, minimap
- Konum: `src/Invekto.Backend/FlowBuilder/`
- Dev: localhost:3002, Serve: Backend:5000/flow-builder/

---

## FB-2: Multi-flow + API + Auth (DONE)

**Tamamlanma:** 2026-02-12 | **Plan:** `arch/plans/20260212-flow-builder-phase2.json`

- DB: chatbot_flows flow_id SERIAL PK, multi-flow per tenant, partial unique is_active
- Automation: 7 CRUD endpoint (list, get, create, update, delete, activate, deactivate)
- Backend: FlowBuilderClient proxy, JWT login (API key from tenant_registry.settings_json)
- SPA: react-router-dom, LoginPage, FlowListPage (full CRUD), FlowEditorPage (API load/save)
- Auth: sessionStorage JWT, 8h expiry, AuthContext + useAuth hook
- Error codes: INV-AT-006 ~ INV-AT-010

---

## FB-2.5: SPA Quick Wins - AHA Moments (DONE)

**Tamamlanma:** 2026-02-13 | **Plan:** `arch/plans/20260213-flow-builder-phase25.json` | **Codex:** 2 iter PASS

> FB-3a backend calismasi oncesi, SPA-only iyilestirmeler. Sifir backend kodu, mevcut API'ler yeterli.

### AHA #6: Kopya Baslat (Flow Duplicate)

**User Pain:** Production'da calisan flow'u duzenlemek riskli. Admin yeni ozellik denemek istiyor ama bozarsa musteriler etkilenir.

**Cozum:** FlowListPage'deki her kartin "..." menusune "Kopyasini Olustur" butonu.

**Davranis:**
1. Mevcut flow'un `flow_config` JSON'unu al
2. `POST /api/v1/flows/{tenantId}` ile yeni flow olustur (mevcut endpoint)
3. Isim: `"{Orijinal Isim} - Kopya"`
4. Otomatik **pasif** olustur
5. Yeni flow'un editor sayfasina yonlendir

**Dosyalar:**
- `FlowListPage.tsx` - Duplicate butonu + handler (mevcut `api.createFlow()` kullanir)

**Metrik:** Production flow'a dokunmadan test etme orani

---

### AHA #2: Kirmizi Kenar - Gercek Zamanli Graph Validation (Lite)

**User Pain:** Admin orphan node (baglantisiz) veya dead-end (cikisi yok) olusturuyor, fark etmiyor. Flow aktive edilince musteri dead-end'e dusuyor.

**Cozum:** Her edge ekleme/silme sonrasi SPA'da anlik graph validation overlay.

**Kurallar (gercek implementasyon):**
| Durum | Gorsel | Severity | Tooltip |
|-------|--------|----------|---------|
| Orphan node (input edge yok, trigger_start degil) | Kirmizi boxShadow (`#ef4444`) | error | "Bu adima ulasilamiyor" |
| Dead-end node (output edge yok, handoff/note degil) | Turuncu boxShadow (`#f97316`) | warning | "Bu adimdan sonra akis duruyor" |
| Bos zorunlu alan (message_text.text bos, menu options bos) | Turuncu boxShadow (`#f97316`) | warning | "Mesaj metni bos" / "Menu secenekleri bos" |

**Davranis (gercek implementasyon):**
- `queueMicrotask` ile 9 mutating action sonrasi deferred revalidation
- Validation sonuclari Zustand store'da `validationErrors: Map<nodeId, ValidationError[]>`
- BaseNode'da boxShadow (selected=blue ring oncelikli, validation ring sadece unselected)
- Native browser tooltip (`title` attribute)

**Dosyalar:**
- `lib/graph-validator.ts` (NEW) - SPA-side graph validation (adjacency list, orphan/dead-end/empty check)
- `store/flow-store.ts` - `validationErrors` state + `revalidate()` action
- `nodes/BaseNode.tsx` - Ring class + tooltip gosterimi

**Metrik:** Hatali flow'un canli ortama cikmadan yakalanma orani

---

### AHA #1: Canli Onizleme - Her Degisiklikte Flow Ozeti

**User Pain:** Admin flow'u kaydediyor ama "bu flow musteriye ne gosterecek?" sorusunun cevabini gormek icin ayri test lazim. Canvas'a bakarak davranisi hayal etmek zor.

**Cozum:** Canvas altinda her zaman gorunur, collapsible "Flow Ozeti" bandi. `useMemo([nodes, edges])` ile her degisiklikte anlik guncellenir.

**Davranis (gercek implementasyon):**
1. `useMemo` ile her node/edge degisikliginde anlik DFS traversal (save beklenmez)
2. `trigger_start` node'undan DFS, menu dallanmalari indent ile
3. Node type ikonlari (play, speech bubble, clipboard, person)
4. Max 5 satir (daha uzunsa "... ve N adim daha")
5. Collapse state localStorage'da persist (`console.warn` on error)

**Dosyalar:**
- `lib/flow-summarizer.ts` (NEW) - DFS traversal + text chain builder + truncateSummary
- `components/FlowSummaryBar.tsx` (NEW) - Always-visible collapsible summary band
- `pages/FlowEditorPage.tsx` - FlowSummaryBar entegrasyonu (canvas altinda flex-col)
- `components/FlowCanvas.tsx` - `h-full` → `min-h-0` (flex-col layout fix)

**Metrik:** Save sonrasi admin'in flow'u yeniden acip kontrol etme orani (azalmali)

---

## FB-3: FlowEngine v2 + Test/Simulasyon (DONE)

**Plan:** `arch/plans/20260213-flow-builder-phase3.json`

### FB-3a: FlowEngine v2 + Validator + Migrator (DONE)

**Tamamlanma:** 2026-02-13 | **Commit:** `74c9ffd` | **Codex:** 3 iter Q FORCE PASS | **Dosya:** 16 dosya +1942 -27

**Mimari Iyilestirmeler (2026-02-13 plan review):**

| # | Iyilestirme | Fayda |
|---|-------------|-------|
| IMP-1 | Node Handler Registry (Strategy) | FB-4'te 7 yeni type eklenirken engine'e dokunulmaz |
| IMP-2 | Immutable Pre-computed Graph | Parse once, O(1) lookup, session boyunca reuse |
| IMP-3 | Expression Safety Limits | ReDoS (100ms), max 50 var, max 10KB value, flat only |
| IMP-4 | ExecutionContext Object | Clean API, loose params yerine tek object |
| IMP-5 | Error Recovery Strategy | Node hatasi → session=error + handoff (sessiz kalma yok) |
| IMP-6 | Migrator Auto-Layout | v1 → v2 sonrasi okunabilir canvas (trigger→welcome→menu→options) |
| IMP-7 | CancellationToken Propagation | Request timeout → engine durdurulur |
| IMP-8 | Pure Engine (Side-Effect Free) | FlowEngineV2 DB/HTTP yapmaz → Simulation icin kritik |

**Yeni dosyalar:**
- `FlowGraphV2.cs` - Immutable in-memory graph (adjacency list, node/edge lookup, static Build)
- `FlowEngineV2.cs` - Pure graph executor (handler registry dispatch, NodeResult[] doner)
- `FlowValidator.cs` - Graph dogrulama (orphan, zorunlu alan, edge tutarliligi, loop detection)
- `FlowMigrator.cs` - v1 menu config → v2 graph donusumu + auto-layout positions
- `ExpressionEvaluator.cs` - Degisken substitution + condition evaluation + safety limits
- `NodeHandlers/INodeHandler.cs` - Handler interface + ExecutionContext + NodeResult
- `NodeHandlers/TriggerStartHandler.cs` - Entry point, otomatik sonraki node'a gec
- `NodeHandlers/MessageTextHandler.cs` - {{variable}} substitution + mesaj uret
- `NodeHandlers/MessageMenuHandler.cs` - Menu goster, kullanici secimi bekle (wait)
- `NodeHandlers/ActionHandoffHandler.cs` - Terminal, session bitir, ozet uret
- `NodeHandlers/UtilityNoteHandler.cs` - No-op, sadece skip

**Degistirilen dosyalar:**
- `AutomationOrchestrator.cs` - Version dispatch (v1 → FlowEngine, v2 → FlowEngineV2) + side-effect yonetimi
- `AutomationRepository.cs` - LogAutoReplyAsync'e node_id parametresi eklenir
- `Automation/Program.cs` - Yeni endpoint'ler (validate, migrate-v1) + handler DI registration

**DB Migration (FB-3a zorunlu):**
- `ALTER TABLE auto_reply_log ADD COLUMN node_id VARCHAR(100)` - v2 flow execution node tracking
- FB-5b AHA #7 (trafik heatmap) bu kolona bagimli. FB-3a'da eklenmezse FB-5b calisamaz.

**Node execution modeli:**
| Tip | Davranis | FB-3a Handler |
|-----|----------|------------------|
| trigger_start | Auto-chain | TriggerStartHandler |
| message_text | Auto-chain | MessageTextHandler |
| message_menu | Wait (input bekle) | MessageMenuHandler |
| action_handoff | Terminal | ActionHandoffHandler |
| utility_note | No-op skip | UtilityNoteHandler |
| action_delay, utility_set_variable, logic_condition, logic_switch | Auto-chain | FB-4 |
| ai_intent, ai_faq | Wait | FB-4 |
| action_api_call | External | FB-4 |

**Error codes:** INV-AT-011 ~ INV-AT-017

### FB-3b: Test Simulasyon API + SPA Chat Panel (Ana Ozellik)

**Amac:** Flow'lari aktive etmeden SPA icinde interaktif test etme

**Scope siniri:** FB-3b'de simulation sadece 5 implemented node type ile calisir (trigger_start, message_text, message_menu, action_handoff, utility_note). ai_faq/ai_intent/logic node'lari FB-4'e kadar SPA'da olusturulamaz - MockFaqMatcher/MockIntentDetector sadece FB-4 sonrasi kullanilabilir.

**Backend:**
- `SimulationEngine.cs` - In-memory test session (DB'ye yazmaz, side-effect yok, **TTL: 30dk, ConcurrentDictionary + cleanup timer**)
- `MockFaqMatcher.cs` - Hardcoded keyword-based mock FAQ (DB sorgusu yok) - FB-4 sonrasi aktif
- `MockIntentDetector.cs` - Rule-based mock intent (Claude API cagrisi yok) - FB-4 sonrasi aktif
- Endpoint'ler: `POST /simulation/start`, `POST /simulation/step`, `DELETE /simulation/{sid}`
- Backend proxy: `/api/v1/flow-builder/simulation/*`

**SPA:**
- `SimulationPanel.tsx` - WhatsApp benzeri chat paneli (editor saginda, 264px)
- `ChatBubble.tsx` - Mesaj baloncugu (user=sag/mavi, bot=sol/gri)
- `simulation-store.ts` - Zustand store (session, messages, currentNodeId, variables)
- Toolbar'a "Test Et" butonu
- FlowCanvas'ta aktif node yesil highlight (ring-4 ring-green-500)
- BaseNode'da pulse animasyonu + play badge
- Interaktif menu secimi (chat'teki butonlara tikla)

**Error codes:** INV-AT-018 ~ INV-AT-020

#### AHA #4: Tek Tikla Test - Save → Simulation Koprusu

**User Pain:** Save ile test arasinda mental kopukluk. Admin save'e basar, sonra ayri "Test Et" butonunu bulup tiklamasi gerekir.

**Cozum:** Save basarili olduktan sonra sag alt kosede toast notification:
```
✅ Kaydedildi  |  [▶ Hemen Test Et]
```

**Davranis:**
1. Save basarili → 5 saniyelik toast goster
2. "Hemen Test Et" butonuna basinca SimulationPanel otomatik acilir
3. Simulation session otomatik baslar (POST /simulation/start)
4. Ilk kez flow kaydeden admin'e SimulationPanel otomatik acilsin (`localStorage` flag: `fb_first_save_done`)
5. Toast kapanirsa normal "Test Et" toolbar butonuyla erisim devam eder

**Dosyalar:**
- `components/SaveToast.tsx` (NEW) - Toast with test button
- `pages/FlowEditorPage.tsx` - Save handler'a toast trigger + localStorage check
- `store/simulation-store.ts` - `autoStartSession()` action

**Metrik:** Save'den ilk test mesajina gecen sure (hedef: 3 saniyenin altina)

---

### FB-3c: Validation UI + Insights + Polish

**Amac:** Validation sonuclarini SPA'da gosterme, debug araclari, flow kalite gorunurlugu

**Mevcut plan:**
- FlowSettingsPanel'e "Akisi Dogrula" butonu + hata/basari gosterimi
- SimulationPanel'e Sohbet/Degiskenler tab switcher
- Variable inspector (runtime degisken goruntuleme)
- Execution path breadcrumb (node ID'leri ok ile)
- Yeniden Baslat butonu

#### AHA #5: Akis Saglik Skoru - Flow Listesinde Kalite Badge

**User Pain:** Flow listesinde sadece isim, node sayisi, aktif/pasif gorunuyor. 5+ flow varsa hangisinin iyi, hangisinin bozuk oldugunu bilmek icin hepsini tek tek acmak lazim.

**Cozum:** FlowListPage'deki her karta "Saglik Skoru" badge'i.

**Skor hesaplama (SPA-side):**
```
skor = (bagli_node_orani × 40) + (dolu_alan_orani × 30) + (handoff_var_mi × 15) + (trigger_var_mi × 15)
```

**Gosterim:**
| Skor | Badge | Renk |
|------|-------|------|
| 90-100 | 🟢 Saglikli | green-500 |
| 60-89 | 🟡 Eksik var | yellow-500 |
| 0-59 | 🔴 Sorunlu | red-500 |

**Onkosul:** List endpoint (`GET /api/v1/flows/{tenantId}`) su anda `flow_config` donmuyor (sadece `node_count`, `edge_count` donuyor). FB-3c'de list endpoint'e `flow_config` JSONB eklenmeli (Karar: 2026-02-13, Q Secenek A).

**Performans notu:** Full flow_config JSONB return etmek 20+ flow'lu tenant'ta yavas olabilir. Alternatif: Backend'de `calculateHealthScore()` hesapla, sadece skor dondir. SPA full config indirmez. Bu kararla list response'a `health_score INT` + `health_issues TEXT[]` eklenir, `flow_config` eklenmez.

**Davranis (guncel):**
1. Backend: `FlowValidator.CalculateHealthScore(flowConfig)` - mevcut validation logic'i yeniden kullan
2. List endpoint response'a `health_score` (0-100) + `health_issues` (string[]) ekle
3. SPA'da badge render (green/yellow/red), hover'da issues listesi
4. Full flow_config indirilmez (performans), backend pre-compute yapar

**Dosyalar:**
- `AutomationRepository.cs` - ListFlows SQL'ine health score pre-compute (veya application-level)
- `Automation/Program.cs` - List response'a health_score + health_issues ekle
- `FlowValidator.cs` (FB-3a'dan mevcut) - `CalculateHealthScore()` metodu eklenir
- `pages/FlowListPage.tsx` - Health badge component + skor gosterimi

**Metrik:** Admin'in sorunlu flow'u tespit etme suresi (0 tikla vs N tikla)

---

#### AHA #3: Ghost Path - Canvas'ta Musteri Yollarini Gorselleştir

**User Pain:** 12+ node'lu karmasik flow'da admin "musteri hangi yollardan gecebilir?" sorusunu cevaplayamiyor. Dallanma arttikca graph okunamaz.

**Cozum:** Toolbar'a "Yollari Goster" toggle butonu. Aktifken tum olasi musteri yollarini renkli overlay olarak canvas'a ciz.

**Davranis:**
1. Toggle aktif → `trigger_start`'tan DFS ile tum path'leri enumerate et
2. Her path farkli renkte soluk overlay (opacity 0.3)
3. Hover'da path detayi: "Yol 1: Hosgeldin → Menu → SSS (3 adim)"
4. Ulasilamayan node'lar gri overlay (`opacity-30`)
5. Max 10 path limiti (combinatorial explosion onleme)
6. Toggle kapaninca overlay temizlenir

**Dosyalar:**
- `lib/path-enumerator.ts` (NEW) - DFS path finder (max 10 path, cycle detection)
- `components/GhostPathOverlay.tsx` (NEW) - React Flow custom edge overlay
- `components/Toolbar.tsx` - Toggle butonu + state

**Metrik:** Karmasik flow'larda admin'in dead-end/unreachable node'u fark etme suresi

---

## FB-4: Genisletilmis Node Tipleri (DONE)

**Amac:** Kalan 7 node tipinin UI property editor'larini ve backend execution handler'larini tamamlama

**IMP-1 (Strategy Pattern) ile:** Her node tipi = 1 SPA property editor + 1 backend NodeHandler .cs

| Node Type | SPA Editor | Backend Handler | Complexity |
|-----------|-----------|-----------------|------------|
| logic_condition | Condition editor (variable, operator, value), true/false handle'lar | `LogicConditionHandler.cs` - ExpressionEvaluator ile condition eval, true/false edge dispatch | LOW |
| logic_switch | Switch editor (variable, cases listesi), N+1 handle | `LogicSwitchHandler.cs` - Variable match, case edge dispatch + default | LOW |
| action_delay | Saniye sayisi editor | `ActionDelayHandler.cs` - Production: Task.Delay, Simulation: instant skip | LOW |
| utility_set_variable | Variable name + expression editor | `SetVariableHandler.cs` - ExpressionEvaluator ile variable assign | LOW |
| ai_intent | Intent listesi editor, confidence threshold, high/low handle'lar | `AiIntentHandler.cs` - Production: IntentDetector (Claude), Simulation: MockIntentDetector | MEDIUM |
| ai_faq | Min confidence editor, matched/no_match handle'lar | `AiFaqHandler.cs` - Production: FaqMatcher (DB), Simulation: MockFaqMatcher | MEDIUM |
| action_api_call | HTTP method/URL/headers/body editor, response variable, success/error handle'lar | `ApiCallHandler.cs` - Production: HttpClient, Simulation: mock response | HIGH |

**Oneri:** FB-4'u 2 sub-phase'e bol:
- **FB-4a** (LOW): logic_condition, logic_switch, action_delay, utility_set_variable (pure logic, external dependency yok)
- **FB-4b** (MEDIUM-HIGH): ai_intent, ai_faq, action_api_call (external service dependency: Claude API, DB, HTTP)

---

## FB-5: iframe Embed + Analytics + Polish (PLANNED)

**Amac:** Main App icine gomme, gercek trafik analizleri ve UX iyilestirmeleri

**Scope notu:** Bu faz cok genis. Sub-phase'lere bolunmesi onerilir:
- **FB-5a** (HIGH): iframe postMessage auth + Main App proxy (core entegrasyon)
- **FB-5b** (MEDIUM): AHA #7 trafik heatmap (analytics, onkosul: FB-3a node_id tracking)
- **FB-5c** (LOW): UX polish (auto-save, shortcuts, export/import, dark mode)
- **FB-5d** (DONE): Deploy script'e FlowBuilder SPA build step eklendi

**Mevcut plan:**
- iframe postMessage authentication (Main App -> Flow Builder)
- Main App proxy authentication (API key yerine)
- Auto-save (debounced, her 30s veya degisiklik sonrasi)
- Dark/light tema toggle
- Keyboard shortcuts (Ctrl+D silme, Ctrl+C/V kopyala/yapistir node)
- Performance optimizasyonu (lazy loading, virtualization)
- Flow export/import (JSON dosya olarak)

#### AHA #7: Son Musteri Yolu - Gercek Trafik Heatmap

**User Pain:** Flow canli ama admin "musteriler gercekte hangi yoldan gidiyor?" bilmiyor. `auto_reply_log` tablosunda veri var ama SQL bilmeden erisilemez.

**Onkosul:** FlowEngineV2 production'da calisiyor + v2 flow'lar aktif + auto_reply_log'da node-level tracking var (FB-3a)

**Cozum:** Aktif flow'un editor sayfasinda "Gercek Trafik" butonu. Tiklayinca canvas edge'leri trafik yogunluguna gore kalinlasir.

**Backend:**
- `GET /api/v1/flows/{tenantId}/{flowId}/analytics?period=24h` (NEW endpoint)
- `auto_reply_log` + `chat_sessions` aggregation: son 24 saat / son 50 conversation
- Response: `{ paths: [{ nodeChain: ["trigger_start_1", "msg_1", "menu_1"], count: 45 }], edgeHits: { "edge_1": 120, "edge_2": 30 } }`

**SPA:**
- `components/TrafficOverlay.tsx` (NEW) - Edge thickness + opacity by hit count
- En cok kullanilan edge: kalin + koyu (stroke-width: 4, opacity: 1.0)
- Az kullanilan edge: ince + soluk (stroke-width: 1, opacity: 0.3)
- Node uzerinde hit count badge: "120 mesaj"
- Insight banner: "Musterilerin %60'i Secenek 2'yi seciyor"

**Dosyalar:**
- `Automation/Services/FlowAnalyticsService.cs` (NEW) - Log aggregation + path extraction
- `Automation/Program.cs` - Analytics endpoint
- `Backend proxy` - Analytics proxy route
- `components/TrafficOverlay.tsx` (NEW) - Heatmap overlay
- `store/flow-store.ts` - Analytics state + fetch action
- `components/Toolbar.tsx` - "Gercek Trafik" toggle butonu

**Metrik:** Admin'in data-driven flow optimizasyonu yapma orani (edge kaldirma/ekleme sonrasi trafik degisimi)

---

## Teknik Stack

| Katman | Teknoloji |
|--------|-----------|
| SPA | React 18 + TypeScript 5.5 + Vite 5.1 |
| Graph | @xyflow/react 12.6 |
| State | Zustand 5.0 |
| Routing | React Router 7.13 |
| Style | TailwindCSS 3.4 |
| Backend | .NET 8 Minimal API |
| DB | PostgreSQL (chatbot_flows JSONB) |
| Auth | JWT (HMAC-SHA256, 8h expiry) |

## Portlar

| Servis | Port | Rol |
|--------|------|-----|
| Backend | 5000 | SPA serve + proxy |
| Automation | 7108 | Flow engine + CRUD + simulation |
| FlowBuilder Dev | 3002 | Vite dev server (sadece local) |

---

## AHA Moment Entegrasyonu

> 2026-02-13 tarihli AHA analizi sonucu eklenen 7 iyilestirme.

| # | Baslik | Phase | Durum | Efor | Etki | Layer | Bagimlilk |
|---|--------|-------|-------|------|------|-------|-----------|
| 1 | Canli Onizleme (anlik flow ozeti) | FB-2.5 | **DONE** | Low | High | SPA-only | Yok |
| 2 | Kirmizi Kenar (graph validation lite) | FB-2.5 | **DONE** | Low | High | SPA-only | Yok |
| 3 | Ghost Path (yol overlay) | FB-3c | **DONE** | Medium | Medium | SPA-only | #2 (graph-validator.ts) |
| 4 | Tek Tikla Test (save→test) | FB-3b | **DONE** | Low | High | SPA-only | FB-3b simulation panel |
| 5 | Akis Saglik Skoru (liste badge) | FB-3c | **DONE** | Low-Med | High | SPA + backend (pre-compute) | #2 + FlowValidator.CalculateHealthScore |
| 6 | Kopya Baslat (flow duplicate) | FB-2.5 | **DONE** | Low | High | SPA + mevcut API | Yok |
| 7 | Son Musteri Yolu (trafik heatmap) | FB-5b | PLANNED | High | High | Full stack | FlowEngineV2 + auto_reply_log.node_id (FB-3a) |

### Kill List (YAPMA)

| Fikir | Neden Yapma |
|-------|-------------|
| Template gallery | 5/12 node tipi calismiyor, template'ler yarisinin bozuk olacagi garanti. FlowEngineV2 + tum node tipleri oncesi deger yok |
| Collaborative editing (CRDT) | Tenant basina genellikle 1 admin. Yillarca ROI'siz muhendislik yatirimi |
| AI-powered flow generation | FlowConfigV2 graph yapisi karmasik (positions, edge routing, handles). LLM uretimi yuksek olasilikla invalid. Once validation + test + tum node tipleri |

### Paylasilan Moduller (AHA'lar Arasi)

```
lib/graph-validator.ts  ← #2 olusturur, #3 ve #5 yeniden kullanir
  - validateGraph()     ← #2: orphan, dead-end, empty field check
  - enumeratePaths()    ← #3: DFS path finder
  - calculateHealth()   ← #5: skor hesaplama

lib/flow-summarizer.ts  ← #1 olusturur
  - summarizeFlow()     ← DFS text chain builder
```
