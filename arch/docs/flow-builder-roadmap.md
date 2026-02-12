# Flow Builder Roadmap

> Tum Flow Builder fazlarinin ozeti. Her fazin detayli plan JSON'u `arch/plans/` altinda.

## Genel Bakis

| Faz | Baslik | Durum | Risk | Plan JSON | AHA |
|-----|--------|-------|------|-----------|-----|
| Phase 1 | SPA UI & Canvas | DONE | LOW | (yok - Phase 2 oncesi) | - |
| Phase 2 | Multi-flow + API + Auth | DONE | HIGH | `20260212-flow-builder-phase2.json` | - |
| Phase 2.5 | SPA Quick Wins (AHA) | PLANNED | LOW | (yok - SPA-only) | #1 #2 #6 |
| Phase 3a | FlowEngine v2 + Validator + Migrator | PLANNED | HIGH | `20260213-flow-builder-phase3.json` | - |
| Phase 3b | Test Simulasyon + Tek Tikla Test | PLANNED | HIGH | (3a ile ayni plan) | #4 |
| Phase 3c | Validation UI + Insights + Polish | PLANNED | MEDIUM | (3a ile ayni plan) | #3 #5 |
| Phase 4 | Genisletilmis Node Tipleri | PLANNED | MEDIUM | (henuz yok) | - |
| Phase 5 | iframe Embed + Analytics + Polish | PLANNED | MEDIUM | (henuz yok) | #7 |

---

## Phase 1: SPA UI & Canvas (DONE)

**Tamamlanma:** 2026-02-12

- React 18 + TypeScript + Vite + TailwindCSS + React Flow (xyflow) + Zustand
- n8n-style gorsel drag-drop chatbot flow editor
- 5 node tipi UI: trigger_start, message_text, message_menu, action_handoff, utility_note
- Property panel (sag taraf), node palette (sol taraf)
- Undo/redo (50 snapshot), edge silme, minimap
- Konum: `src/Invekto.Backend/FlowBuilder/`
- Dev: localhost:3002, Serve: Backend:5000/flow-builder/

---

## Phase 2: Multi-flow + API + Auth (DONE)

**Tamamlanma:** 2026-02-12 | **Plan:** `arch/plans/20260212-flow-builder-phase2.json`

- DB: chatbot_flows flow_id SERIAL PK, multi-flow per tenant, partial unique is_active
- Automation: 7 CRUD endpoint (list, get, create, update, delete, activate, deactivate)
- Backend: FlowBuilderClient proxy, JWT login (API key from tenant_registry.settings_json)
- SPA: react-router-dom, LoginPage, FlowListPage (full CRUD), FlowEditorPage (API load/save)
- Auth: sessionStorage JWT, 8h expiry, AuthContext + useAuth hook
- Error codes: INV-AT-006 ~ INV-AT-010

---

## Phase 2.5: SPA Quick Wins - AHA Moments (PLANNED)

**Oncelik:** Quick Win | **Efor:** LOW | **Risk:** LOW | **Backend degisiklik:** YOK (sadece #6 icin mevcut API kullanilir)

> Phase 3a backend calismasi oncesi, SPA-only iyilestirmeler. Sifir backend kodu, mevcut API'ler yeterli.

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

**Kurallar:**
| Durum | Gorsel | Tooltip |
|-------|--------|---------|
| Orphan node (input edge yok, trigger_start degil) | Kirmizi ring (`ring-2 ring-red-500`) | "Bu adima ulasilamiyor" |
| Dead-end node (output edge yok, handoff/note degil) | Turuncu ring (`ring-2 ring-orange-400`) | "Bu adimdan sonra akis duruyor" |
| Bos zorunlu alan (message_text.text bos) | Sari ring (`ring-2 ring-yellow-400`) | "Mesaj metni bos" |

**Davranis:**
- `onEdgesChange` ve `onNodesChange` event'lerinde adjacency check
- Validation sonuclari Zustand store'da `validationErrors: Map<nodeId, string[]>`
- BaseNode component'inde conditional ring class

**Dosyalar:**
- `lib/graph-validator.ts` (NEW) - SPA-side graph validation (adjacency list, orphan/dead-end/empty check)
- `store/flow-store.ts` - `validationErrors` state + `revalidate()` action
- `nodes/BaseNode.tsx` - Ring class + tooltip gosterimi

**Metrik:** Hatali flow'un canli ortama cikmadan yakalanma orani

---

### AHA #1: Canli Onizleme - Save Sonrasi Flow Ozeti

**User Pain:** Admin flow'u kaydediyor ama "bu flow musteriye ne gosterecek?" sorusunun cevabini gormek icin ayri test lazim. Canvas'a bakarak davranisi hayal etmek zor.

**Cozum:** Save basarili olduktan sonra canvas altinda 3 satirlik "Flow Ozeti" bandi.

**Ornek cikti:**
```
📱 Musteri mesaj atar → "Hos geldiniz!" → 3 secenekli menu →
   Sec.1: SSS yaniti | Sec.2: Operatore aktarim | Sec.3: AI yanit
```

**Davranis:**
1. Save basarili → `trigger_start` node'undan DFS traversal
2. Her node'un `data.text` veya `data.label`'ini zincirle
3. Dallanma varsa (menu options, condition) → indent ile goster
4. Max 5 satir (daha uzunsa "... ve N adim daha")
5. Canvas altinda `FlowSummaryBar.tsx` component'i (collapse/expand)

**Dosyalar:**
- `lib/flow-summarizer.ts` (NEW) - DFS traversal + text chain builder
- `components/FlowSummaryBar.tsx` (NEW) - Collapsible summary band
- `pages/FlowEditorPage.tsx` - Save sonrasi summary trigger

**Metrik:** Save sonrasi admin'in flow'u yeniden acip kontrol etme orani (azalmali)

---

## Phase 3: FlowEngine v2 + Test/Simulasyon (PLANNED)

**Plan:** `arch/plans/20260213-flow-builder-phase3.json`

### Phase 3a: FlowEngine v2 + Validator + Migrator (Backend Only)

**Amac:** v2 graph-based execution engine, validation, v1 migration

**Yeni dosyalar:**
- `FlowGraphV2.cs` - In-memory graph (adjacency list, node/edge lookup)
- `FlowEngineV2.cs` - Node-by-node graph executor (chain vs wait nodelari)
- `FlowValidator.cs` - Graph dogrulama (orphan, zorunlu alan, edge tutarliligi)
- `FlowMigrator.cs` - v1 menu config -> v2 graph donusumu
- `ExpressionEvaluator.cs` - Degisken substitution + condition evaluation

**Degistirilen dosyalar:**
- `AutomationOrchestrator.cs` - Version dispatch (v1 -> FlowEngine, v2 -> FlowEngineV2)
- `Automation/Program.cs` - Yeni endpoint'ler (validate, migrate-v1)

**Node execution modeli:**
| Tip | Davranis |
|-----|----------|
| trigger_start, message_text, action_delay, utility_set_variable, utility_note, logic_condition, logic_switch | Auto-chain (hemen isle, sonrakine gec) |
| message_menu, ai_intent, ai_faq | Wait (kullanici girdisi bekle) |
| action_handoff | Terminal (session bitir) |
| action_api_call | External (HTTP call, success/error edge) |

**Error codes:** INV-AT-011 ~ INV-AT-017

### Phase 3b: Test Simulasyon API + SPA Chat Panel (Ana Ozellik)

**Amac:** Flow'lari aktive etmeden SPA icinde interaktif test etme

**Backend:**
- `SimulationEngine.cs` - In-memory test session (DB'ye yazmaz, side-effect yok)
- `MockFaqMatcher.cs` - Hardcoded keyword-based mock FAQ (DB sorgusu yok)
- `MockIntentDetector.cs` - Rule-based mock intent (Claude API cagrisi yok)
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

### Phase 3c: Validation UI + Insights + Polish

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

**Onkosul:** List endpoint (`GET /api/v1/flows/{tenantId}`) su anda `flow_config` donmuyor (sadece `node_count`, `edge_count` donuyor). Phase 3c'de list endpoint'e `flow_config` JSONB eklenmeli (Karar: 2026-02-13, Q Secenek A).

**Davranis:**
1. List endpoint'e `flow_config` alanini ekle (backend degisiklik: `AutomationRepository.ListFlows` + `Program.cs` response)
2. SPA'da her flow icin `calculateHealthScore(flowConfig)` cagir
3. Phase 2.5'teki `graph-validator.ts` yeniden kullanil (orphan/dead-end/empty check)
4. Badge hover'da detay: "2 orphan node, 1 bos mesaj"

**Dosyalar:**
- `AutomationRepository.cs` - ListFlows response'a flow_config ekle
- `Automation/Program.cs` - List endpoint response'a flow_config ekle
- `lib/graph-validator.ts` (Phase 2.5'ten mevcut) - `calculateHealthScore()` eklenir
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

## Phase 4: Genisletilmis Node Tipleri (PLANNED)

**Amac:** Kalan 7 node tipinin UI property editor'larini ve backend execution'larini tamamlama

- logic_condition: Condition editor (variable, operator, value), true/false handle'lar
- logic_switch: Switch editor (variable, cases listesi), N+1 handle
- ai_intent: Intent listesi editor, confidence threshold, high/low handle'lar
- ai_faq: Min confidence editor, matched/no_match handle'lar
- action_api_call: HTTP method/URL/headers/body editor, response variable, success/error handle'lar
- action_delay: Saniye sayisi editor
- utility_set_variable: Variable name + expression editor

---

## Phase 5: iframe Embed + Analytics + Polish (PLANNED)

**Amac:** Main App icine gomme, gercek trafik analizleri ve UX iyilestirmeleri

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

**Onkosul:** FlowEngineV2 production'da calisiyor + v2 flow'lar aktif + auto_reply_log'da node-level tracking var

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

| # | Baslik | Phase | Efor | Etki | Layer | Bagimlilk |
|---|--------|-------|------|------|-------|-----------|
| 1 | Canli Onizleme (save ozeti) | 2.5 | Low | High | SPA-only | Yok |
| 2 | Kirmizi Kenar (graph validation lite) | 2.5 | Low | High | SPA-only | Yok |
| 3 | Ghost Path (yol overlay) | 3c | Medium | Medium | SPA-only | #2 (graph-validator.ts) |
| 4 | Tek Tikla Test (save→test) | 3b | Low | High | SPA-only | Phase 3b simulation panel |
| 5 | Akis Saglik Skoru (liste badge) | 3c | Low-Med | High | SPA + backend (list endpoint) | #2 (graph-validator.ts) + list endpoint flow_config |
| 6 | Kopya Baslat (flow duplicate) | 2.5 | Low | High | SPA + mevcut API | Yok |
| 7 | Son Musteri Yolu (trafik heatmap) | 5 | High | High | Full stack | FlowEngineV2 production |

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
