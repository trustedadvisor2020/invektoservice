# Respond.io vs Invekto — Competitive Analysis

**Tarih:** 2026-04-24
**Rakip hesap:** regexbackup@gmail.com (Growth Plan free trial, 2026-05-01 bitiş)
**Workspace ID:** 408881 ("My New Workspace")
**Bağlı kanal:** Telegram (`@Backup1TestBot`)
**Canlı test:** Q kendi Telegram'ından 3 mesaj attı, full flow gözlemlendi
**Yöntem:** Playwright (headed Chrome + persistent profile) + manual screenshot review
**Dosyalar:** `tracking/competitor-respond-io/` — 20 script, 200+ ekran görüntüsü, 15+ JSON veri dump

---

## TL;DR — En Kritik 10 Bulgu

1. **AI-first UX.** AI Agents sidebar'da top-level modül, **AI Prompts** (change tone / translate / fix spelling / simplify) + **AI Assist** (knowledge-grounded reply) + **Summarize** (konuşma özeti) compose area'da tek tıkla, **AI Copilot** help bubble platform kullanımında rehber. Invekto'da bunların hiçbiri yok.
2. **Lifecycle çapraz veri modeli.** New Lead → Hot Lead → Payment → Customer + Cold Lead (Lost), Inbox/Contacts/Dashboard/Reports/Segments hepsinde aynı alan. Invekto'da "pipeline" kavramı var ama bu kadar çapraz değil.
3. **Multi-channel single conversation.** Aynı contact için Telegram + WhatsApp + Email tek thread, compose'da kanal seçerek yanıt. Invekto'da kanal başına ayrı thread (verify).
4. **Slash commands inline.** `/` → snippet, `$` → variable ($contact.firstname vs.), `::` → emoji shortcode. Prompt placeholder'da direkt yazıyor.
5. **Workflows builder mature.** 14+ action (Send Message, Ask Question, Assign, Branch, Update Field/Tag/Lifecycle, Wait, HTTP Request, Google Sheets Row, Meta/TikTok Conversions API, Trigger Another Workflow), 18+ template kategorisi, visual canvas + right-panel config.
6. **Broadcast modülü yok-denli feature.** Segment-based targeting, channel type (specific vs. last-interacted), Table + Calendar view, scheduling/status filters. Invekto'da mass-messaging hiç yok.
7. **Reports 11 bağımsız dashboard.** Lifecycle, Calls, Conversations, Responses, Resolutions, Messages, Contacts, Assignments, **Leaderboard (User + AI Agent)**, Users, Broadcasts — her biri kendi filtre seti.
8. **3 katmanlı settings mimarisi.** Organization (402285) / Workspace / Personal. Growth plan **MAC = 1,000 (Monthly Active Contacts)** usage metric — Invekto'da kullanım bazlı metrik yok.
9. **Zero-touch onboarding.** Contact auto-created from Telegram display name, Language = auto-detected, Phone region inferred, Lifecycle Stage auto-assigned, channel events otomatik logged.
10. **Onboarding UX zengin.** 4 adımlı progress checklist (0 of 4), Resources panel (Watch intro / Support / Book demo / Help center / Video guides), her modülde coach mark tour overlay.

---

## Status

| Phase | Scope | Status | Screenshot klasör |
|-------|-------|--------|-------------------|
| 1 | Setup + Login (Google SSO başarısız → direkt email/şifre OK) | ✅ | `screenshots/` |
| 2 | Sidebar top-level crawl (8 module) | ✅ | `phase2/` |
| 3 | Inbox initial | ✅ | `phase3-inbox/` |
| 3.5 | Workspace Settings full nav (17 bölüm) | ✅ | `phase3_5-settings/` |
| 9 | Connect Telegram + `@Backup1TestBot` | ✅ | `phase9-telegram/` |
| 9.5 | Inbox live with real 3 mesaj | ✅ | `phase9_5-inbox-live/` |
| 9.6 | AI Assist live test (slash/dollar/emoji + AI Prompts menu) | ✅ | `phase9_6-ai-assist/` |
| 5 | Workflows (template library + builder + action catalog) | ✅ | `phase5-workflows/` |
| 6 | Broadcasts (new flow, segment, channel type, calendar) | ✅ | `phase6-broadcasts/` |
| 4 | Contacts (Trusted Advisor detail, segment builder) | ✅ | `phase4-contacts/` |
| 11 | Thorough sweep (AI Copilot help bubble, Reports 11 tabs, onboarding modal accordions, personal settings) | ✅ | `phase11-sweep/` |
| 10 | Final comparison + action list | ✅ bu belge | — |

---

## Bölüm 1 — Respond.io Feature Map

### 1.1 Sidebar (8 ana modül)

```
Onboarding         /onboarding          — getting-started checklist + Resources
Dashboard          /dashboard           — Lifecycle funnel KPI + Contacts + Team
Inbox              /inbox               — Unified conversations
Contacts           /contact             — CRM + Lifecycle + Segments
AI Agents (Beta)   /ai-agents           — 3 hazır agent tipi (Agent/Sales/Support)
Broadcast          /broadcast           — Mass message campaigns
Workflows          /workflows           — Visual flow builder
Reports            /reports             — 11 sub-dashboard
```

Sidebar bottom: Settings gear • Notification bell • Help chat bubble (AI Copilot) • respond.io logo.
Sidebar sadece **ikon** — label yok, hover tooltip bile gecikmeli. Newcomer için öğrenme maliyeti var.

### 1.2 Workspace Settings (17 bölüm)

URL kalıbı: `/space/{workspace_id}/settings/{section}`

| Bölüm | Invekto karşılığı | Notlar |
|-------|---|---|
| General info | Tenant settings ✅ | Workspace name / timezone / currency |
| User settings | User list ✅ | Invite + roles |
| Team settings | ⚠️ kısmen | Team grouplar |
| **Channels** | ⚠️ kısmen | 11+ kanal (WhatsApp API/Cloud, TikTok, FB, IG, Telegram, Viber, LINE, WeChat, Custom, SMS, Email, Live Chat) |
| **Integrations** | ❌ zayıf | **Salesforce / HubSpot / Google Sheets / Make / Zapier / Webhooks / Developer API** native |
| **Growth widgets** | ❌ YOK | Web site embed lead capture widget'ları |
| Contact fields | ⚠️ custom fields | Şema seviyesinde custom alan tanımı |
| **Lifecycle** | ❌ YOK | Drag-drop stage builder, Won/Lost ayrı, toggle göster/gizle |
| Closing notes | ⚠️ kısmen | Zorunlu not kategorileri chat kapanışında |
| Snippets | ⚠️ template kısmen | Canned response + inbox inline `/` autocomplete |
| Tags | ✅ var | Tag sözlüğü |
| **AI Assist** | ❌ YOK | Reply outside knowledge toggle + Use snippets as knowledge + **AI Persona prompt** (“You will be a seasoned customer support agent…”) + knowledge source upload |
| **AI Prompts** | ❌ YOK | Change tone / Translate / Fix spelling / Simplify toggle'ları + custom prompt |
| Calls | ⚠️ TONIVA? | Voice calls **NEW** badge |
| Files | ✅ var | Workspace dosya deposu |
| Contacts import | ⚠️ CSV import var | Alan eşleme ekranı |
| Data export | ⚠️ kısmen | Bulk export / GDPR |

### 1.3 Üç Katman Settings + MAC Usage

- Organization settings (org_id=402285, "backup") — üst seviye, multi-workspace yönetimi
- Workspace settings — yukarıdaki 17 bölüm
- Personal settings (user menu dropdown: Profile, Notification settings, Online/Away, Sign out)

**MAC = Monthly Active Contacts.** Growth plan = 1,000/ay. Invekto'da benzeri yok — user seat + storage ağırlıklı. **Bu pricing metric, değer bazlı (ne kadar aktif müşteri ile konuşuyorsun) — SaaS'ta modern yaklaşım.**

### 1.4 Inbox — Conversation Anatomy (Canlı Veri)

**Sol panel (Inbox nav):**
- All 1, Mine, Unassigned 1, Incoming Calls, **Create AI Agent** (Beta)
- Lifecycle Stages filtreleri: New Lead 1 / Hot Lead / Payment / Customer
- Team Inbox (+) / Custom Inbox (+) — kullanıcı tanımlı queue'lar

**Chat liste:** Avatar + contact name + last msg preview + time + Lifecycle pill + unread badge + assignment indicator.

**Üst bar:** Contact name + **Lifecycle pill (tıkla = değiştir)** + chevron expand + Unassigned dropdown + search + clock (history) + **call icon (Telegram içinden voice?)** + **"Close" button** (conversation kapatma, dedicated status) + `...` menu.

**Chat akışı:**
- "Today" separator
- System events inline: *Welcome Event*, *Lifecycle Stage New Lead added*, *Conversation opened by Contact*
- Customer bubbles (sol, avatar + tick indicator)

**Compose area (çok zengin):**
- **Channel selector dropdown** (Telegram / başka kanal) — multi-channel conversation
- Placeholder: **"Use '/' for snippets, '$' for variables, '::' for emoji"**
- Icon bar (soldan sağa): magic wand (AI Prompts) / smiley (emoji) / knowledge? / globe / snippet / code-bracket
- Sağ üst: **✨ AI Assist** (knowledge-grounded reply)
- Sağ alt: **"Press 'Enter' to send"** + mavi send ok
- Alt tab: **"Add comment"** — internal team note modu (Quill + @mention destekli) + **✨ Summarize** (konuşma özeti AI)

**Right panel:** icon-only column (Contact details / Phone log / Journey / Clock / vs.). Tıklayınca açılır.

**Otomatik davranışlar (zero-touch):**
- Contact otomatik yaratıldı (Telegram display name = "Trusted Advisor", ID=435207950)
- Phone: TR +90 (bölge inferred)
- Language: English (Telegram locale auto-detected)
- Lifecycle: New Lead otomatik eklendi
- Proactive modal: "Your channel is live! Create an AI Agent to reply instantly…" CTA

### 1.5 AI Özellikleri (5 ayrı AI touch-point!)

1. **AI Agents modülü** (top-level, Beta) — 3 hazır persona: Agent / Sales / Support. Knowledge sources + conversation auto-assign. "Assigns conversations to other agents, teams and AI Agents" (AI-to-AI handoff).
2. **AI Assist** (settings + inline button) — Persona prompt + knowledge sources + "reply outside of knowledge" toggle (ChatGPT fallback) + "use snippets as knowledge" toggle.
3. **AI Prompts** (settings + compose wand icon) — Change tone (submenu), Translate (submenu), Fix spelling & grammar, Simplify language + "Add AI prompt" custom.
4. **Summarize** (compose bottom-right) — konuşma özeti AI buton.
5. **AI Copilot** (help bubble) — ChatGPT-tarzı in-product asistan, "Create an AI Agent for me", "How do I connect a channel", vs. yapabiliyor. Platformu senin adına konfigure edebilir.

**Invekto'da karşılık:** `InvektoChatAnalysis` servisi var (session memory verilerinde geçiyor), ama kullanıcının gördüğü UI seviyesinde AI rewrite / AI persona / AI assist button / AI copilot help yok. En yakını TFM (Topluluk Flow Modülü) — ama o da workflow, "AI-in-compose" değil.

### 1.6 Workflows — Builder

**Template library (18+ kategori):**
Assignment (Open Contacts / Round Robin / Round Robin + Online Only / Unassigned Conversation Closed) • Away Message (with/without Business Hours) • Broadcast Response • Click to Chat Ads • Sales Call Report • TikTok Report New Leads • Welcome Message (& Ask for Email) • Unsubscribe from Broadcasts • **Contact Routing: By Language Preference / CTC Info Ads Route to Sales / By Sub-Menu Choice** • Issue Escalation • Multi-Level Chat Menu (Main/Sub/Team Routing)

**Builder canvas:**
- Visual node-based (Zapier/n8n-tarzı)
- Top bar: workflow name + last updated + **Save / Test / Publish** buttons (3-state deploy)
- Left: zoom/fit + undo/redo
- Canvas: dotted grid, merkez Trigger node + chain with `+` node'lar
- Right: Trigger/Action config panel + "Advanced Settings: Trigger once per contact" toggle

**Action Catalog (14+ confirmed, bazıları paid gated):**
Send a Message • Ask a Question • Assign To • Branch (koşullu dallanma) • Update Contact Field • Update Contact Tag • Open Conversation • Wait (sleep) • **Send Conversions API Event (Meta)** • **Send TikTok Lower Funnel Event** • **Trigger Another Workflow** (nested) • **HTTP Request** (🔒 UPGRADE required) • Add Google Sheets Row • Update Lifecycle

Invekto TFM'de Branch / Wait / HTTP / nested trigger var mı? `arch/specs/` altında kontrol edilmeli.

### 1.7 Broadcast

- Status filtreleri: All / Draft / Scheduled / In Progress / Completed / Failed
- Table + Calendar görünüm toggle
- Create flow:
  1. Modal: "Broadcast Name" + "Labels (optional)" + **Cancel / Create** (Cancel text butonu + X var — Q'nun UX kuralına aykırı)
  2. Config page: **Segment** dropdown (+ Add Segment ile inline yarat) + **Channel Type** radio (Specific channel / **Last interacted channel** — çoklu kanal routing), Message Content (+ Add Content)
  3. Top: Save / **Next** (flow)
- Trial kısıtı: "Sending or scheduling broadcast is not available in Trial. You can still use **Test Broadcast**."

### 1.8 Reports (11 sub-dashboard)

Her biri bağımsız, **tarih filtresi + Add filter + Clear all + Group By** standart kontrolleri:

| Report | KPI cards | Charts |
|--------|-----------|--------|
| Lifecycle | Overall conversion / Avg time to conversion / Overall drop-off / Avg time to drop-off | Journey Funnel + Lost Stages Breakdown |
| Calls | Total incoming / Total outgoing / Avg duration | Incoming (Answered/Missed) + more |
| Conversations | — | Volume, open/closed |
| Responses | Response time | — |
| Resolutions | Resolution time | — |
| Messages | Messages sent/received | — |
| Contacts | Growth | — |
| Assignments | — | — |
| **Leaderboard** | — | **Conversations Assigned (Group By: User and AI Agent)** + Conversations Closed |
| Users | User activity | — |
| Broadcasts | Broadcast perf | — |

**Leaderboard'da AI Agent = user-like entity** — AI'ın kaç konuşma halletmiş metriği ölçülüyor. Invekto'da "AI vs human" ayrımı yok.

### 1.9 Contacts — Detail + Segments

**Contact detail (sağ drawer):**
- Avatar + Name + ID + Channels row (Telegram icon)
- Assignee dropdown + Lifecycle pill (değişebilir)
- **Contact fields** (+ Manage link → shema editörü):
  - Phone Number (country code + number)
  - Email Address
  - Country
  - Language
  - + custom fields (settings/contact_fields)
- Tags + button

**Pre-built Segments (5 adet):**
- Contacts created <7 days
- Contacts inactive >30 days
- Contacts with tags
- Country known
- Language known
- + "+ Add segment" custom filter builder

**Lifecycle Stages (settings ekranı):**
- Drag-drop reorder
- Her stage: icon + name + "Show description" link + "..." menu
- **Won stage** işaretleyebilirsin (Customer = Won Stage)
- **Lost Stages** ayrı kolon (Cold Lead)
- Show/Hide Lifecycle toggle (tamamen kapat)

### 1.10 Onboarding

Sticky banner: **"Your Growth Plan trial ends in 7 days on May 01, 2026. Upgrade now to avoid service disruption!"** (agresif upsell)

**Onboarding checklist modal (top-right button):**
- Progress: "0 of 4"
- 4 accordion (her biri illüstrasyon + bullet features + CTA):
  1. **Connect Channels to unify messaging and calls** — WhatsApp/Messenger/Telegram/IG/TikTok/LINE/Viber/WeChat/Gmail ikonları + "Connect Channel"
  2. **Learn how Lifecycle helps turn potential buyers into customers**
  3. **Set up AI Agents to lighten your team's workload** — 4 bullet (Replies 24/7 / Updates Lifecycle + Contact fields automatically / Assigns conversations / Answers from uploaded business info) + "Set up AI Agent"
  4. **Invite teammates to collaborate on chats**

**Resources panel (sağ):**
- Watch Inbox intro (video)
- Contact support
- Book a demo
- Help center
- Video guides

**Her modülde coach mark tour overlay** — dismissable, "1/3", "2/3" slayt. Invekto'da bu yok (veya çok az).

### 1.11 Diğer

- **Help chat bubble = AI Copilot Beta** (purple smiley, sağ alt). ChatGPT-tarzı platform asistanı + "Create AI Agent for me" gibi action'lar. "AI Copilot is in Beta and may make mistakes."
- **Notification bell** — system notifications (empty trial'da).
- **Upgrade now banner** sürekli sticky.

---

## Bölüm 2 — Invekto'nun Respond.io'dan Daha İyi Olabileceği Yerler

*(Tahmin — CLAUDE.md + session memory + session içi gözlemler bazlı; kod review gerekir)*

1. **Türkiye lokalizasyonu + Türkçe UI** — respond.io EN-dominant.
2. **INMA entegrasyonu & multi-tenant provisioning** — Invekto'nun domain spesifik avantajı.
3. **Voice (TONIVA / SIP / MicroSIP)** — respond.io'da Calls bölümü "New Beta", üstelik "disabled for this Workspace" diyor. Invekto'nun ses altyapısı çok daha olgun (MicroSIP C++ native client).
4. **Daha az sticky upsell** — respond.io her ekranda "Upgrade now" banner, Invekto'da bu rahatsızlık yok.
5. **Modal X vs Cancel kuralı** — respond.io çoğu yerde **Cancel + X** ikisini birden koyuyor (Q'nun UX preference'ına göre X yeterli, Cancel kötü).

---

## Bölüm 3 — Gereksiz Karmaşık / Zayıf Yönler (Respond.io)

| Sorun | Detay |
|-------|-------|
| Sidebar sadece ikon | Label yok, hover tooltip gecikmeli, yeni kullanıcıya eziyet |
| Agresif upsell | Trial banner sticky + popup modals canlı müşteri konuşmasında bile ("Create an AI Agent") |
| System events chat içinde inline | "Welcome Event", "Lifecycle Stage added" müşteri mesajlarıyla karışıyor — ayrı timeline olmalı |
| Compose area yoğun | 6+ icon + dropdown + 2 tab + 2 AI button = overload |
| Onboarding tour dismiss kalıcı değil | Workflows'a her gidişte yine 1/3 overlay gelebiliyor |
| Settings 3 katman ayrımı | Organization vs Workspace vs Personal — hangi ayar nerede bulmak için öğrenme şart |
| HTTP Request upgrade-gated | Custom integration yapmak için ücretli plan şart, freemium çekici değil |
| "Cancel + X" modallarda | X'e basarken Cancel'a basma riski UX hatası |
| Right panel icon-only | Hover'sız ne olduğu belirsiz |
| Quill editor `::` için `:` placeholder | Placeholder'da `:smi` yazınca shortcode çıkıyor ama `::` yazmak şart — tutarsız |

---

## Bölüm 4 — Gap Analysis: Invekto'nun Yapmadığı / Yapması Gereken

### P0 — Çekirdek Eksikler (Strategic, 1-2 ay)

| # | Özellik | Respond.io nasıl yapmış | Invekto için eylem |
|---|---------|--------------------------|---------------------|
| P0-1 | **Lifecycle cross-cutting data model** | Contacts'ta stage, Inbox filter, Reports funnel, Segments targeting — hepsi tek enum | `arch/db/` → Contact.lifecycle_stage enum; UI'a drag-drop stage builder; Reports funnel dashboard |
| P0-2 | **AI Prompts in compose** (Change tone / Translate / Fix spelling / Simplify) | 4 built-in + custom, per-workspace toggle | Invekto Chat compose'a wand icon + 4 prompt + `ai-rewrite` service endpoint (mevcut `InvektoChatAnalysis` ile entegre) |
| P0-3 | **AI Assist with persona + knowledge sources** | Settings'te persona prompt + knowledge upload + "reply outside of knowledge" toggle | Knowledge base feature + per-workspace AI persona (tenant-scoped) + inline "AI Assist" reply suggestion button |
| P0-4 | **Broadcast module (mass messaging)** | Segment + Channel Type + Message + Schedule/Send | Yeni servis `InvektoBroadcast` + UI (list/calendar/create/builder) — MAC/günlük limit + compliance (opt-out) |
| P0-5 | **Slash commands in compose** (/snippet, $variable, ::emoji) | Quill tabanlı editor, autocomplete popover | Compose editor upgrade (Quill veya tiptap) + snippet service + variable interpolation runtime |

### P1 — Yüksek Değer (3-6 ay)

| # | Özellik | Respond.io | Invekto action |
|---|---------|------------|----------------|
| P1-1 | **Multi-channel single conversation** | Aynı contact için TG + WA + Email tek thread | Conversation aggregation layer (contact_id by channel → unified) |
| P1-2 | **AI Copilot help bubble** | In-product ChatGPT asistan, "Create AI Agent for me" | Knowledge base + LLM + tool-calling → platform actions |
| P1-3 | **Segments + Segment builder** | Condition builder, pre-built 5 segment, cross-module usage | `segments` tablosu + query builder UI + Broadcast/Workflow tetikçisi |
| P1-4 | **Workflow action catalog genişletme** | Branch, Wait, HTTP, Meta Conversions API, Google Sheets Row, Trigger Another Workflow | TFM'ye bu action'lar — özellikle HTTP + Meta/TikTok conversion event entegrasyonu |
| P1-5 | **Growth Widgets** | Web embed lead capture | Embeddable chat widget + lead capture form builder |
| P1-6 | **Reports modül genişletme (11 dashboard)** | Lifecycle/Calls/Conv/Response/Resolution/Message/Contact/Assignment/**Leaderboard/User+AI**/Broadcast | Per-area report pages + date filter + Group By + AI-vs-human breakdown |
| P1-7 | **Onboarding checklist + coach marks** | 4-step progress + module tours | Onboarding state machine + coach mark component library |
| P1-8 | **MAC usage metering** | 1,000 MAC/ay pricing metric | Usage table + billing integration + overlimit UX |

### P2 — Polish & Nice-to-have (6+ ay)

| # | Özellik | Notlar |
|---|---------|--------|
| P2-1 | **Conversation "Close" status** | Dedicated closed state (Invekto "resolve" var mı verify) |
| P2-2 | **Internal comment / team note** | Quill editor + @mention (Invekto'da ayrı tab olarak) |
| P2-3 | **Template library (Workflows)** | 18+ hazır template ile onboarding hızı |
| P2-4 | **Calendar view (Broadcasts)** | Timeline visualization |
| P2-5 | **Auto-enrichment from messenger** | Language/Country/Timezone auto-detect Telegram locale'den |
| P2-6 | **AI Persona tenant-scoped** | Settings'te custom persona prompt + knowledge upload |
| P2-7 | **Integrations marketplace** | Native Salesforce/HubSpot/Google Sheets/Zapier/Make cards |
| P2-8 | **Multi-workspace per Organization** | Org-Workspace-User 3 katman (Invekto'da tenant = workspace, parent org yok) |
| P2-9 | **Custom Inbox / Team Inbox** | Kullanıcı tanımlı queue, filter saved views |
| P2-10 | **Calls module** | Invekto'da TONIVA var ama UI entegrasyonu zayıf (verify) |

---

## Bölüm 5 — "Respond.io'nun Yapıp Invekto'nun Yapmaması Kritik Olanlar"

Eğer **tek seçmem gereken 3 özellik** olsa, şu sırayla gelirdi:

1. **AI Prompts in compose (P0-2)** — en az maliyetli, en yüksek kullanıcı-görünür değer. Ajanın yazdığı metnin tonunu değiştirme / dil düzeltme / çevirme — her mesajda kullanır.
2. **Lifecycle cross-cutting (P0-1)** — veri modeli değişikliği, ama tüm ürünün satış-odaklı hissetmesini sağlar (Reports funnel, Contacts stages, Inbox filter).
3. **Broadcast module (P0-4)** — mass messaging + Telegram/WA toplu bildirim — growth/retention için SaaS'ta standart.

---

## Bölüm 6 — Bot Token / Cleanup Önerisi

- Bot: `@Backup1TestBot`, token chat'te geçti → **BotFather `/revoke`** ile token yenileme önerilir
- Gmail (regexbackup@gmail.com) → şifre chat context'inde → değiştirme önerilir
- Playwright profile: `c:/tmp/respondio-profile` — analiz bittiyse silinebilir
- Screenshots: `c:/CRMs/InvektoServices/tracking/competitor-respond-io/screenshots/` — rapor için referans, git'e eklenmez (gitignore içinde olması gerekir, kontrol et)

---

## Bölüm 7 — Appendix: Screenshot & Data Index

### Dosyalar
```
tracking/competitor-respond-io/
├── 01-login.js …………………………………… Google SSO login (başarısız)
├── 02-login-direct.js ……………………… Direct email/pass login (başarılı)
├── 03-crawl-sidebar.js ……………………… Phase 2 sidebar top-level crawl
├── 04-inbox-deep.js ………………………… Phase 3 inbox
├── 05-07-settings-*.js …………………… Phase 3.5 settings harvest (3 iter)
├── 08-settings-via-inbox.js ……………… ✅ working settings nav harvester
├── 09-telegram-connect.js ………………… ✅ Telegram bot token bağlama
├── 10-11-inbox-live*.js …………………… Phase 9.5 live conversation
├── 12-ai-assist-live.js …………………… Phase 9.6 AI Prompts menu + slash/dollar/emoji
├── 13-15-workflow*.js ……………………… Phase 5 workflows + template + action catalog
├── 16-17-broadcasts*.js …………………… Phase 6 broadcasts
├── 18-contacts.js …………………………… Phase 4 contacts detail + segment
├── 19-thorough-sweep.js …………………… Phase 11 help bubble + reports 11 tabs + onboarding modal
└── 20-final-gaps.js …………………………… Son eksikler (AI Assist/Summarize button fail, trigger dropdown fail)

notes/
├── sidebar.json ………………………………… 8 top-level modül
├── workspace-settings-nav.json …………… 17 settings bölümü URL'leri
├── workspace-settings-visited.json ……… her birinin sub-tab + mainHeader
├── channel-catalog.json ………………………… kanal listesi (boş, modal başka yere gitti)
├── compose-icons.json ………………………… 11 compose icon position
├── compose-buttons.json ……………………… butonlar + tooltips
├── topbar-buttons.json ………………………… konuşma top bar
├── sidebar-tooltips.json ……………………… her sidebar icon hover tooltip
├── workflow-triggers.json ……………………… (boş — dropdown açılamadı)
├── workflow-actions.json ……………………… (boş; ekran görüntülerinde manuel görüldü)
├── broadcast-config.json ……………………… config page fields
├── contact-fields.json ………………………… contact detail labels
├── add-contact-modal.json ……………………… modal inputs
├── segment-builder.json ………………………… segment modal
├── org-settings-nav.json ………………………… (başarısız)
├── ai-menu.json ……………………………………… AI prompts sub-items
├── ai-assist-panel.json ………………………… (boş — button match sorunu)
└── summarize-output.json ………………………… (boş — button match sorunu)
```

### Key Screenshots (referans için)

| Dosya | Göster |
|-------|--------|
| `phase9-telegram/06-after-submit.png` | Telegram bağlandı QR + t.me link |
| `phase9_5-inbox-live/v2-02-conversation-open.png` | Canlı conversation full layout |
| `phase9_6-ai-assist/03-slash.png` | `/` Snippets menu autocomplete |
| `phase9_6-ai-assist/04-dollar.png` | `$` Variable menu (contact.firstname vs.) |
| `phase9_6-ai-assist/05-emoji-shortcode.png` | `::smi` emoji shortcode |
| `phase9_6-ai-assist/07-icon-0-icon-0.png` | AI Prompts menu (Change tone/Translate/Fix/Simplify) |
| `phase5-workflows/wf2-05-after-plus.png` | Add Steps action catalog |
| `phase5-workflows/02-template-library.png` | 18+ kategori template library |
| `phase3_5-settings/ws-ai-assist.png` | AI Persona + knowledge + snippets toggle |
| `phase3_5-settings/ws-ai-prompts.png` | AI Prompts 4 built-in + Add AI prompt |
| `phase3_5-settings/ws-lifecycle.png` | Lifecycle drag-drop builder |
| `phase3_5-settings/ws-integrations.png` | Salesforce/HubSpot/Google Sheets/Make/Zapier/Webhooks |
| `phase11-sweep/B-01-help-chat.png` | AI Copilot help bubble |
| `phase11-sweep/D-02-accordion-set-up-ai-agents.png` | Onboarding AI Agent detay |
| `phase11-sweep/F-01-report-leaderboard.png` | Leaderboard "User and AI Agent" grouping |
| `phase4-contacts/ct-03-trusted-advisor-detail.png` | Contact detay + auto-enrichment |
| `phase6-broadcasts/bc-02-calendar.png` | Broadcast calendar view |
| `phase6-broadcasts/bc2-04-channel-dropdown.png` | Broadcast segment dropdown |

---

**Sonuç:** Respond.io **AI-first + Lifecycle-centric + full-featured multi-channel** bir conversational CRM. Invekto çekirdeği sağlam (mikroservis mimari, Türkiye fokus, INMA entegrasyonu, ses altyapısı) ama **ürün-pazarlama seviyesinde AI/Broadcast/Lifecycle kapıları henüz açılmamış**. P0 listesindeki 5 özellik kapatılırsa parity sağlanır; P1 ile önde olunabilir.
