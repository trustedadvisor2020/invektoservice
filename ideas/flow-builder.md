# Flow Builder — Visual Chatbot Flow Designer

> **Roadmap Konumu:** Phase 1 → **GR-1.1 Chatbot / Flow Builder** → Sub-phases FB-1 ~ FB-5
> **Roadmap Referans:** [phases/phase-1.md](phases/phase-1.md) (master tracking)
> **Durum:** FB-1 ✅ + FB-2 ✅ tamamlandi, FB-3~5 bekliyor
> **Konum:** `src/Invekto.Backend/FlowBuilder/` (bagimsiz SPA)
> **Bagli Servisler:** Automation:7108 (flow engine), Backend:5000 (proxy + serve)
> **Plan Tarihi:** 2026-02-12
>
> **NOT:** Bu dosya flow builder'in teknik detay dokumanidir. Durum takibi ve
> onceliklendirme icin [phases/phase-1.md](phases/phase-1.md) GR-1.1 bolumune bakin.
> Iki dosya ayni sub-phase numaralarini kullanir (FB-1 ~ FB-5).

---

## Problem

Chatbot engine (Automation:7108) production'da calisiyor. Mevcut flow yapi basit: welcome → menu → action (reply/faq/handoff/intent). Flow config JSONB olarak PostgreSQL'de (`chatbot_flows.flow_config`).

**Sorun:** Tenant'larin chatbot akisini gorsel olarak tasarlayacak bir arac yok. Flow config elle JSON ile yonetiliyor.

**Cozum:** n8n benzeri drag-drop visual flow builder + flow yonetim ekrani.

---

## Neden InvektoServices'te?

| Soru | Cevap |
|------|-------|
| Neden bagimsiz SPA? | Main app'tan bagimsiz, tekrar gelistirme yok |
| iframe ile embed? | Evet — herhangi bir uygulamaya `<iframe>` ile gomulur |
| Standalone calisir mi? | Evet — kendi login sayfasi, `/flow-builder/` URL'i |
| Multi-app? | Invekto Main App, TONIVA, veya baska herhangi bir uygulama |
| Neden Automation:7108 ile ayni repo? | Flow config'i Automation'in DB'sinde (`chatbot_flows`), ayni contract |

---

## Mimari

```
┌──────────────────────────────────────────────────────────────────┐
│                    Main App (veya herhangi bir host)              │
│                                                                  │
│  <iframe src="https://services.invekto.com:5000/flow-builder/">  │
│      │                                                           │
│      │ postMessage: { token, tenantId, origin }                  │
│      ▼                                                           │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │  Flow Builder SPA (React + xyflow + Zustand)             │    │
│  │  Serve: Backend:5000/flow-builder/                       │    │
│  │  Build: wwwroot/flow-builder/                            │    │
│  └──────────┬───────────────────────────────────────────────┘    │
└─────────────│────────────────────────────────────────────────────┘
              │ HTTP (JWT)
              ▼
┌──────────────────────────────┐     ┌──────────────────────────────┐
│  Backend:5000                │     │  Automation:7108             │
│  /api/v1/flow-builder/*      │────▶│  /api/v1/flows/*             │
│  (JWT proxy)                 │     │  (localhost-only)            │
└──────────────────────────────┘     └──────┬───────────────────────┘
                                            │
                                            ▼
                                    ┌──────────────────┐
                                    │  PostgreSQL       │
                                    │  chatbot_flows    │
                                    │  flow_config JSONB│
                                    └──────────────────┘
```

---

## Tech Stack

| Katman | Teknoloji |
|--------|-----------|
| Frontend | React 18 + TypeScript + Vite + TailwindCSS |
| Canvas | React Flow (@xyflow/react) — node/edge visual editor |
| State | Zustand — undo/redo, selection, dirty tracking |
| Serve | Backend:5000 → `wwwroot/flow-builder/` (static) |
| Dev | Vite dev server, port 3002 |
| Auth (iframe) | postMessage ile JWT token |
| Auth (standalone) | Kendi login sayfasi, sessionStorage |
| Backend proxy | Backend:5000 → Automation:7108 (localhost-only) |
| DB | `chatbot_flows.flow_config` JSONB (version field ile v1/v2 ayrim) |
| Engine | FlowEngine v1 (mevcut), FlowEngine v2 (yeni — graph traversal) |

---

## Contract v2 — Node/Edge Graph Model

v1'in yerine gecen graph-based model. React Flow'un data modeli ile 1:1 uyumlu.

### v1 (Mevcut — Menu-Based)

```json
{
  "version": 1,
  "welcome_message": "Hosgeldiniz!",
  "menu": {
    "text": "Secim yapin:",
    "options": [
      { "key": "1", "label": "Randevu", "action": "reply", "reply_text": "..." },
      { "key": "2", "label": "Fiyat", "action": "faq" },
      { "key": "3", "label": "Temsilci", "action": "handoff" }
    ]
  },
  "off_hours_message": "Mesai disi...",
  "unknown_input_message": "Anlayamadim."
}
```

**Limitasyonlar:** Tek seviye menu, dallanma yok, zincirleme mesaj yok, degisken yok.

### v2 (Yeni — Graph-Based)

```json
{
  "version": 2,
  "metadata": {
    "name": "Klinik Flow",
    "description": "Dis klinigi chatbot akisi",
    "canvas_viewport": { "x": 0, "y": 0, "zoom": 1 }
  },
  "nodes": [
    {
      "id": "trigger_start_1",
      "type": "trigger_start",
      "position": { "x": 300, "y": 50 },
      "data": { "label": "Baslangic" }
    },
    {
      "id": "message_text_1",
      "type": "message_text",
      "position": { "x": 300, "y": 150 },
      "data": { "label": "Hosgeldin", "text": "Hosgeldiniz! Size nasil yardimci olabilirim?" }
    },
    {
      "id": "message_menu_1",
      "type": "message_menu",
      "position": { "x": 300, "y": 300 },
      "data": {
        "label": "Ana Menu",
        "text": "Secim yapin:",
        "options": [
          { "key": "1", "label": "Randevu", "handle_id": "opt_1" },
          { "key": "2", "label": "Fiyat", "handle_id": "opt_2" },
          { "key": "3", "label": "Temsilci", "handle_id": "opt_3" }
        ]
      }
    },
    {
      "id": "ai_faq_1",
      "type": "ai_faq",
      "position": { "x": 100, "y": 500 },
      "data": { "label": "Fiyat FAQ", "min_confidence": 0.3 }
    },
    {
      "id": "action_handoff_1",
      "type": "action_handoff",
      "position": { "x": 500, "y": 500 },
      "data": { "label": "Temsilciye Aktar", "summary_template": "Musteri temsilci talep etti." }
    }
  ],
  "edges": [
    { "id": "e1", "source": "trigger_start_1", "target": "message_text_1" },
    { "id": "e2", "source": "message_text_1", "target": "message_menu_1" },
    { "id": "e3", "source": "message_menu_1", "sourceHandle": "opt_2", "target": "ai_faq_1" },
    { "id": "e4", "source": "message_menu_1", "sourceHandle": "opt_3", "target": "action_handoff_1" }
  ],
  "settings": {
    "off_hours_message": "Mesai disi...",
    "unknown_input_message": "Anlayamadim.",
    "handoff_confidence_threshold": 0.5,
    "session_timeout_minutes": 30,
    "max_loop_count": 10
  }
}
```

### Node Turleri (12 Adet)

| Kategori | Type | Aciklama | Output Handle'lar | Phase |
|----------|------|----------|-------------------|-------|
| **Trigger** | `trigger_start` | Entry point (1 adet/flow) | 1 (default) | 1 ✅ |
| **Message** | `message_text` | Metin mesaj gonder | 1 (default) | 1 ✅ |
| **Message** | `message_menu` | Menu goster (butonlar) | N (her option icin) | 1 ✅ |
| **Action** | `action_handoff` | Temsilciye aktar (terminal) | 0 | 1 ✅ |
| **Utility** | `utility_note` | Gorsel yorum (calistirilmaz) | 0 | 1 ✅ |
| **Logic** | `logic_condition` | If/else dallanma | 2 (true/false) | 4 |
| **Logic** | `logic_switch` | Multi-branch switch | N+1 (case + default) | 4 |
| **AI** | `ai_intent` | Claude intent detection | 2 (high/low confidence) | 4 |
| **AI** | `ai_faq` | FAQ arama | 2 (matched/no_match) | 4 |
| **Action** | `action_api_call` | Webhook/HTTP call | 2 (success/error) | 4 |
| **Action** | `action_delay` | Bekle (N saniye) | 1 (default) | 4 |
| **Utility** | `utility_set_variable` | Session degisken ata | 1 (default) | 4 |

### v1 ↔ v2 Backward Compatibility

- `chatbot_flows.flow_config` JSONB'de `version` field ile ayrim
- `version: 1` → mevcut FlowEngine (dokunulmaz, calismaya devam eder)
- `version: 2` → FlowEngineV2 (yeni graph traversal)
- AutomationOrchestrator version check yapip dogru engine'e yonlendirir
- Flow builder acildiginda v1 config otomatik v2'ye cevrilerek gosterilir (save edilene kadar v1 kalir)
- **DB schema degisikligi GEREKMEZ** — mevcut `chatbot_flows` tablosu yeterli

---

## Mevcut Sistemle Entegrasyon

### 1. Veri Akisi

```
chatbot_flows tablosu (PostgreSQL)
    │
    ├── flow_config (JSONB) ← v1 VEYA v2 JSON
    ├── is_active (boolean)
    ├── tenant_id (PK, FK → tenant_registry)
    ├── created_at
    └── updated_at

Mevcut endpoint'ler (Automation:7108):
    GET  /api/v1/flows/{tenantId}  → flow_config + is_active dondurur
    PUT  /api/v1/flows/{tenantId}  → flow_config + is_active upsert

Mevcut proxy (Backend:5000):
    POST /api/v1/automation/webhook → Automation'a mesaj iletir
```

### 2. Yeni Endpoint'ler

**Automation:7108 (localhost-only, yeni):**

| Method | Path | Amac |
|--------|------|------|
| `POST` | `/api/v1/flows/{tenantId}/validate` | Flow graph validation (kaydetmeden) |
| `POST` | `/api/v1/flows/{tenantId}/activate` | is_active toggle |
| `POST` | `/api/v1/flows/{tenantId}/migrate-v1` | v1 → v2 preview (save etmez) |

**Backend:5000 (JWT-protected, proxy):**

| Method | Path | Proxy To |
|--------|------|----------|
| `GET` | `/api/v1/flow-builder/flows/{tenantId}` | Automation GET flows |
| `PUT` | `/api/v1/flow-builder/flows/{tenantId}` | Automation PUT flows |
| `POST` | `/api/v1/flow-builder/flows/{tenantId}/validate` | Automation validate |
| `POST` | `/api/v1/flow-builder/flows/{tenantId}/activate` | Automation activate |
| `POST` | `/api/v1/flow-builder/flows/{tenantId}/migrate-v1` | Automation migrate |

### 3. FlowEngine v2 Entegrasyonu

```
Musteri mesaj gonderir
    │
    ▼
AutomationOrchestrator.ProcessMessageAsync()
    │
    ├── flow_config.version == 1 → FlowEngine (mevcut, dokunulmaz)
    │
    └── flow_config.version == 2 → FlowEngineV2 (yeni)
        │
        ├── FlowGraphV2 — in-memory adjacency list
        ├── chat_sessions.current_node → node ID (orn: "message_menu_1")
        │
        ├── Auto-traverse node'lar (hemen sonrakine gec):
        │   message_text, logic_condition, set_variable, action_delay
        │
        ├── Wait-point node'lar (dur, kullanici inputu bekle):
        │   message_menu, ai_faq, ai_intent
        │
        └── Terminal node'lar (session biter):
            action_handoff
```

**Session State:** `chat_sessions.current_node` v2'de node ID saklar (orn: `"message_menu_1"`)
**Degiskenler:** `chat_sessions.session_data` JSONB → `{ "flow_version": 2, "variables": { ... } }`
**DB degisikligi:** YOK — mevcut kolonlar yeterli.

### 4. iframe Entegrasyonu (Main App)

```
Main App HTML:
  <iframe
    id="flow-builder"
    src="https://services.invekto.com:5000/flow-builder/"
    style="width:100%; height:100%; border:none;"
  />

Main App JS:
  // Flow Builder hazir olunca "ready" mesaji gelir
  window.addEventListener('message', (e) => {
    if (e.data.source === 'flow-builder' && e.data.type === 'ready') {
      // JWT token + tenant bilgisi gonder
      iframe.contentWindow.postMessage({
        type: 'init',
        source: 'host',
        payload: { token: jwt, tenantId: 42, origin: window.origin }
      }, 'https://services.invekto.com:5000');
    }
  });
```

**postMessage Protokolu:**

| Yön | Type | Payload | Amac |
|-----|------|---------|------|
| Host → FB | `init` | `{ token, tenantId, origin }` | JWT + tenant context |
| Host → FB | `set_theme` | `{ theme: 'dark' }` | Tema degistir |
| FB → Host | `ready` | `{ version }` | SPA yuklendi, `init` bekliyor |
| FB → Host | `auth_required` | `{}` | Token expired, yenisini iste |
| FB → Host | `flow_saved` | `{ tenantId, isActive }` | Kaydedildi |
| FB → Host | `flow_dirty` | `{ isDirty }` | Kaydedilmemis degisiklik var |
| FB → Host | `error` | `{ code, message }` | Hata bildirim |

---

## Flow Yonetim Ekrani (CRUD + Test)

Flow Builder'in sol tarafinda veya ayri bir sayfa olarak **flow yonetim paneli** gerekli.
Tenant'in birden fazla flow'u olabilir (ileride), ancak v1'de **tenant basina tek flow** (mevcut DB yapisi).

### Yonetim Ekrani Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  Flow Yonetimi                                         [+ Yeni] │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │  📋 Klinik Ana Flow                                        │  │
│  │  Aciklama: Dis klinigi chatbot akisi                       │  │
│  │  Durum: ● Aktif    Node: 8    Edge: 7    v2                │  │
│  │  Son guncelleme: 12.02.2026 14:30                          │  │
│  │                                                            │  │
│  │  [Duzenle]  [Test Et]  [Aktif/Pasif]  [Kopyala]  [Sil]    │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │  📋 Mesai Disi Flow                                        │  │
│  │  Aciklama: Gece mesajlari icin basit yonlendirme           │  │
│  │  Durum: ○ Pasif    Node: 3    Edge: 2    v2                │  │
│  │  Son guncelleme: 10.02.2026 09:15                          │  │
│  │                                                            │  │
│  │  [Duzenle]  [Test Et]  [Aktif/Pasif]  [Kopyala]  [Sil]    │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │  📋 Eski Flow (v1)                              ⚠️ Legacy  │  │
│  │  Aciklama: Otomatik import edilen v1 flow                  │  │
│  │  Durum: ○ Pasif    Menu: 5 secenek    v1                   │  │
│  │                                                            │  │
│  │  [v2'ye Cevir]  [Goruntule]  [Sil]                         │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### CRUD Islemleri

| Islem | Aciklama | API |
|-------|----------|-----|
| **Listele** | Tenant'in tum flow'larini goster (ad, durum, node/edge sayisi, versiyon, son guncelleme) | GET `/flows/{tenantId}` |
| **Olustur** | Yeni bos flow olustur (default trigger_start node ile) | PUT `/flows/{tenantId}` |
| **Duzenle** | Flow Builder editor'u ac (canvas + palette + properties) | GET + PUT |
| **Sil** | Flow'u tamamen sil (onay dialogu ile) | DELETE `/flows/{tenantId}` |
| **Kopyala** | Mevcut flow'u yeni isimle klonla | GET + PUT (yeni tenant/flow) |
| **Aktif/Pasif** | is_active toggle (sadece 1 flow aktif olabilir) | POST `/flows/{tenantId}/activate` |
| **v2'ye Cevir** | v1 flow'u v2 formatina donustur (preview + onay) | POST `/flows/{tenantId}/migrate-v1` |

### Test Modu

Flow Builder icinde **canli test paneli** — kullanici gibi mesaj yazarak flow'u simule et.

```
┌──────────────────────────────────────────┐
│  🧪 Flow Test                     [Kapat] │
├──────────────────────────────────────────┤
│                                          │
│  Bot: Hosgeldiniz! Size nasil            │
│       yardimci olabilirim?               │
│                                          │
│  Bot: Secim yapin:                       │
│       1. Randevu                         │
│       2. Fiyat                           │
│       3. Temsilci                        │
│                                          │
│  Sen: 2                                  │
│                                          │
│  Bot: [FAQ arama yapiliyor...]           │
│  Bot: Dis beyazlatma 3000-5000 TL       │
│       arasi degismektedir.               │
│                                          │
│  ─── Aktif Node: ai_faq_1 ──────────────│
│  ─── Session Data: { ... } ─────────────│
│                                          │
│  ┌──────────────────────────┐ [Gonder]   │
│  │ Mesajinizi yazin...      │            │
│  └──────────────────────────┘            │
│                                          │
│  [Bastan Basla]  [Adim Adim]  [Kaydet]   │
└──────────────────────────────────────────┘
```

**Test Modu Ozellikleri:**

| Ozellik | Aciklama |
|---------|----------|
| Canli simülasyon | Kaydetmeden mevcut canvas'taki flow'u test et |
| Aktif node highlight | Canvas'ta o an hangi node calisiyorsa yesil border |
| Session state goster | current_node, variables, history |
| Adim adim mod | Her node'da dur, manual ilerle (debugging) |
| Bastan basla | Session sifirla, flow'u yeniden baslat |
| Test log | Tum mesajlari ve node gecislerini logla |

**Test API (Backend uzerinden):**

| Method | Path | Amac |
|--------|------|------|
| `POST` | `/api/v1/flow-builder/flows/{tenantId}/test` | Flow'u test mesaji ile calistir |

Request:
```json
{
  "flow_config": { ... },
  "message": "2",
  "session_state": { "current_node": "message_menu_1", "variables": {} }
}
```

Response:
```json
{
  "replies": ["Dis beyazlatma 3000-5000 TL arasi..."],
  "next_node": "ai_faq_1",
  "session_state": { "current_node": "ai_faq_1", "variables": {} },
  "traversal_log": [
    { "node": "message_menu_1", "action": "option_selected", "option": "2" },
    { "node": "ai_faq_1", "action": "faq_search", "result": "matched" }
  ]
}
```

### Validation (Kayit Oncesi Kontrol)

Flow kaydedilmeden once graph validation calisir:

| Kural | Seviye | Aciklama |
|-------|--------|----------|
| trigger_start var mi? | ERROR | Her flow'da 1 adet olmali |
| trigger_start'tan path var mi? | ERROR | En az 1 edge cikis olmali |
| Orphan node var mi? | WARNING | Hicbir edge'e bagli olmayan node |
| Terminal node'a ulasiliyor mu? | WARNING | Handoff veya bitis noktasi |
| Dongu var mi? | WARNING | Sonsuz loop riski (max_loop_count ile korunur) |
| Bos mesaj text | ERROR | message_text.text bos olamaz |
| Menu seceneksiz | ERROR | message_menu.options bos olamaz |
| Bagli olmayan handle | WARNING | Menu option handle'ina edge baglanmamis |

---

## DB Yapisi

### Mevcut (Degisiklik Yok)

```sql
-- chatbot_flows: 1:1 per tenant (mevcut)
CREATE TABLE chatbot_flows (
    tenant_id    INTEGER PRIMARY KEY,
    flow_config  JSONB NOT NULL DEFAULT '{}'::jsonb,
    is_active    BOOLEAN NOT NULL DEFAULT false,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- chat_sessions: state tracking (mevcut)
CREATE TABLE chat_sessions (
    id              SERIAL PRIMARY KEY,
    tenant_id       INTEGER NOT NULL,
    chat_id         INTEGER NOT NULL,
    phone           VARCHAR(20),
    current_node    VARCHAR(50) NOT NULL DEFAULT 'welcome',  -- v2'de node ID olur
    session_data    JSONB NOT NULL DEFAULT '{}'::jsonb,       -- v2'de { flow_version: 2, variables: {} }
    status          VARCHAR(20) NOT NULL DEFAULT 'active',
    started_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_activity_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() + INTERVAL '30 minutes')
);
```

**v2 Farki:** `current_node` v1'de "welcome"/"menu"/"faq" gibi sabit string, v2'de "message_menu_1" gibi node ID. `session_data`'ya `flow_version` + `variables` eklenir. DB schema degisikligi **gerekmez**.

### Gelecek: Multi-Flow Destegi (Opsiyonel, Phase 5+)

Eger tenant basina birden fazla flow istenirse:

```sql
-- chatbot_flows tablosuna eklenir:
ALTER TABLE chatbot_flows ADD COLUMN flow_id SERIAL;
ALTER TABLE chatbot_flows DROP CONSTRAINT chatbot_flows_pkey;
ALTER TABLE chatbot_flows ADD PRIMARY KEY (tenant_id, flow_id);
-- + is_default kolonu eklenir
```

> **Simdilik:** Tenant basina 1 flow yeterli. Multi-flow ihtiyaci geldiginde schema genisler.

---

## SPA Dosya Yapisi (Mevcut)

```
src/Invekto.Backend/FlowBuilder/
├── package.json              # @xyflow/react, zustand, react, tailwindcss
├── vite.config.ts            # base: '/flow-builder/', dev port 3002
├── tsconfig.json
├── tailwind.config.js
├── index.html
└── src/
    ├── main.tsx              # React entry
    ├── App.tsx               # Auth routing (iframe vs standalone)
    ├── index.css             # xyflow base + tailwind + dark theme
    ├── types/
    │   └── flow.ts           # Contract v2 types (12 node type, NodeData union)
    ├── lib/
    │   ├── utils.ts          # cn(), generateNodeId(), generateEdgeId()
    │   ├── api.ts            # [Phase 2] FlowBuilderApiClient (JWT)
    │   ├── iframe-bridge.ts  # [Phase 5] postMessage protocol
    │   ├── auth.ts           # [Phase 2] iframe/standalone auth
    │   └── v1-migrator.ts    # [Phase 3] v1 → v2 conversion
    ├── store/
    │   └── flow-store.ts     # Zustand: nodes, edges, selection, undo/redo (max 50)
    ├── nodes/                 # Custom React Flow node components
    │   ├── index.ts          # nodeTypes registry
    │   ├── BaseNode.tsx      # Shared wrapper (blue input, green output handles)
    │   ├── TriggerStartNode.tsx   # ✅ Green, no input
    │   ├── MessageTextNode.tsx    # ✅ Blue, text preview
    │   ├── MessageMenuNode.tsx    # ✅ Blue, multi-output per option
    │   ├── ActionHandoffNode.tsx  # ✅ Red, terminal (no output)
    │   └── UtilityNoteNode.tsx    # ✅ Sticky-note, configurable color
    ├── panels/
    │   ├── NodePropertyPanel.tsx  # Sag panel: node ozellikleri edit
    │   └── FlowSettingsPanel.tsx  # Genel flow ayarlari
    ├── components/
    │   ├── FlowCanvas.tsx         # React Flow canvas (drag-drop, delete)
    │   ├── NodePalette.tsx        # Sol sidebar: kategorili node listesi
    │   ├── Toolbar.tsx            # Ust bar: ad, undo/redo, save
    │   └── DeleteEdgeButton.tsx   # Hover X ile edge silme
    └── pages/
        ├── FlowEditorPage.tsx     # Ana editor layout
        ├── FlowListPage.tsx       # [Phase 2] Flow yonetim ekrani
        └── LoginPage.tsx          # [Phase 2] Standalone login
```

---

## Uygulama Fazlari

### FB-1: SPA Scaffold + Canvas ✅ TAMAMLANDI (GR-1.1 tasks 1.1.1~1.1.8)

**Yapilan:**
- [x] SPA projesi (package.json, vite, tailwind, tsconfig)
- [x] React Flow + Zustand + TailwindCSS kurulumu
- [x] Contract v2 TypeScript types (12 node type)
- [x] Zustand store (nodes, edges, selection, undo/redo max 50)
- [x] 5 node component: trigger_start, message_text, message_menu, action_handoff, utility_note
- [x] FlowCanvas: drag-drop, self-connection prevention
- [x] NodePalette: kategorili sol sidebar
- [x] NodePropertyPanel: type-specific editors (menu options CRUD, color picker, etc.)
- [x] FlowSettingsPanel: genel flow ayarlari
- [x] Toolbar: flow adi/aciklama, undo/redo, save, dirty indicator
- [x] Custom edge: hover X ile baglanti silme
- [x] Handle renkleri: input=blue, output=green
- [x] Build PASS (tsc 0 error, vite build OK — JS 368KB gzip 118KB)
- [x] Q UI test OK

### FB-2: API + Backend Entegrasyon ✅ TAMAMLANDI (GR-1.1 tasks 1.1.9~1.1.15)

| # | Gorev | Detay |
|---|-------|-------|
| 7 | SPA fallback route | Backend:5000 `/flow-builder/{**slug}` → index.html |
| 8 | JWT prefix | `/api/v1/flow-builder/` JWT korumasi |
| 9 | FlowBuilderClient.cs | Backend → Automation proxy class |
| 10 | Proxy endpoint'ler | 5 endpoint (GET/PUT flows, validate, activate, migrate-v1) |
| 11 | Automation yeni endpoint'ler | validate, activate, migrate-v1 |
| 12 | SPA API client | `lib/api.ts` — load/save flow, JWT header |
| 13 | Flow yonetim ekrani | FlowListPage: liste, aktif/pasif toggle, sil, v1 goster |
| 14 | Auth | Standalone login + iframe postMessage |

### FB-3: FlowEngine v2 — Backend Execution (GR-1.1 tasks 1.1.16~1.1.21)

| # | Gorev | Detay |
|---|-------|-------|
| 15 | FlowGraphV2.cs | In-memory adjacency list, node lookup |
| 16 | FlowEngineV2.cs | Node executor + chain traversal |
| 17 | FlowValidator.cs | Graph validation rules (orphan, cycle, empty) |
| 18 | FlowMigrator.cs | v1 → v2 otomatik conversion |
| 19 | Orchestrator dispatch | version check → v1 veya v2 engine |
| 20 | Error codes | INV-AT-006 ~ INV-AT-010 |

### FB-4: Genisletilmis Node'lar (GR-1.1 tasks 1.1.22~1.1.26)

| # | Gorev | Detay |
|---|-------|-------|
| 21 | Logic: condition, switch | If/else + multi-branch |
| 22 | AI: intent, faq | Mevcut IntentDetector/FaqMatcher reuse |
| 23 | Action: api_call, delay | Webhook + bekleme |
| 24 | Utility: set_variable | Session degisken sistemi |
| 25 | Node component'leri | 7 yeni React Flow node component |
| 26 | Property panel editors | Logic/AI/Action/Utility icin ozellik editoru |

### FB-5: iframe + Polish (GR-1.1 tasks 1.1.27~1.1.32)

| # | Gorev | Detay |
|---|-------|-------|
| 27 | iframe bridge | postMessage protocol (init, ready, auth_required, flow_saved) |
| 28 | Auto-detection | `window.self !== window.top` → iframe mode |
| 29 | Tema destegi | dark/light theme switching |
| 30 | Auto-save | Debounced save (5s idle) |
| 31 | Keyboard shortcuts | Ctrl+S, Ctrl+Z, Ctrl+Y, Delete |
| 32 | Flow validation UI | Inline hata/uyari overlay |
| 33 | Test modu | Canli flow simülasyonu (chat panel + node highlight) |
| 34 | MiniMap iyilestirme | Tum node type'lari icin renk |

---

## Error Codes

| Code | HTTP | Mesaj | Durum |
|------|------|-------|-------|
| INV-AT-006 | 400 | Flow validation failed | Graph validation hatalari |
| INV-AT-007 | 404 | Flow not found for tenant | Tenant'in flow'u yok |
| INV-AT-008 | 409 | Flow version conflict | Esanli kaydetme catismasi |
| INV-AT-009 | 400 | Invalid flow config version | Desteklenmeyen version |
| INV-AT-010 | 500 | Flow engine execution error | v2 engine runtime hatasi |

---

## Basari Kriterleri

| Kriter | Olcut |
|--------|-------|
| Build | `npm run build` 0 error |
| SPA serve | `http://localhost:5000/flow-builder/` editor gorunur |
| CRUD | Flow olustur/duzenle/sil/listele calisir |
| Save/Load | Save → Reload → ayni flow geri gelir |
| v1 migration | v1 flow acilinca otomatik v2 olarak gosterilir |
| Engine v2 | v2 config ile mesaj gonderince dogru node traversal |
| iframe | `<iframe>` + postMessage init → editor calisir |
| Test modu | Mesaj yaz → dogru cevap + node highlight |
| Backward compat | v1 flow'lar mevcut FlowEngine ile calismaya devam eder |

---

## Referanslar

- Plan dosyasi: `C:\Users\taner\.claude\plans\abstract-moseying-hoare.md`
- v1 contract: `arch/contracts/automation-flow.json`
- DB schema: `arch/db/automation.sql`
- Error codes: `arch/errors.md`
- Mevcut FlowEngine: `src/Invekto.Automation/Services/FlowEngine.cs`
- Mevcut Orchestrator: `src/Invekto.Automation/Services/AutomationOrchestrator.cs`
- Mevcut flow endpoint'ler: `src/Invekto.Automation/Program.cs` (GET/PUT `/api/v1/flows/{tenantId}`)
- Roadmap: `ideas/roadmap.md` (Phase 1 — GR-1.1 Chatbot/Flow Builder)
