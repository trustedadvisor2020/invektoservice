# Phase 0: Pre-Paket (Tekli Dongu)

> Stage-0 scaffold, GR-0.x validasyon, GR-1.x servisler, WA pipeline, Knowledge RAG, Flow Builder.
> Tarih: 2026-02-01 ~ 2026-02-15

## Tamamlanan Isler

### Stage-0 Scaffold (01-02 Sub)
- Backend (port 5000) + ChatAnalysis (port 7101) + Shared library
- /health, /ready, /ops endpoint'leri
- JSON Lines logger, NSSM Windows Service
- WapCRM + Claude Haiku chat analysis entegrasyonu

### GR-0.1~0.6: Niche Validasyonu (Phase 0)
- **GR-0.1** E-ticaret Niche: Trendyol/HB satıcıları (50-500 sipariş/gün), 10 görüşme, fiyat 3-5K TL/ay
- **GR-0.2** Diş Kliniği Niche: 2-5 ünitelik klinikler, 10 görüşme, fiyat 7.5K TL/ay
- **GR-0.3** Estetik Klinik Niche: botox/dolgu/lazer/saç ekimi, 10 görüşme, fiyat 15-25K TL/ay
- **GR-0.4** Grand Slam Offer: 3 niche offer (sonuç vaadi + garanti + kıtlık)
- **GR-0.5** Mevcut Müşteri Analizi: 50+ müşteri, churn/retention, AI pricing model
- **GR-0.6** İlk Müşteri: 3 niche'te ilk ödeme alındı

### GR-1.9 Integration Bridge (08 Sub)
- JWT auth middleware (HMAC-SHA256)
- Webhook receiver + async callback client
- PostgreSQL connection factory
- Plan: `arch/plans/20260208-integration-bridge.json`

### GR-1.1 Automation Service - Port 7108 (09 Sub)
- Menu chatbot engine, FAQ automation, Claude Haiku intent
- Mesai disi oto-cevap, human handoff
- Plan: `arch/plans/20260209-automation-service.json`

### GR-1.2 AgentAI Service - Port 7105 (11 Sub)
- AI reply suggestion + intent detection
- Per-agent feedback learning (son 20 interaction)
- TemplateEngine.cs: `{{variable}}` substitution + HTML sanitization
- Plan: `arch/plans/20260211-agentai-service.json`

### GR-1.3 Outbound Service - Port 7107 (12 Sub)
- Broadcast/bulk messaging (max 1000 recipient, async queue)
- TriggerProcessor: manual, new_lead, payment_received, appointment_reminder
- RateLimiter: sliding window, 30 msg/min/tenant
- OptOutManager: STOP/DUR/İPTAL keyword detection
- DB: outbound_templates, outbound_broadcasts, outbound_messages, outbound_optouts
- Plan: `arch/plans/20260212-outbound-service.json`

### GR-1.10 Ops Dashboard Log Iyilestirmesi (14 Sub)
- LogEntry category alanı (api, system, step)
- LogReader category filtresi (?category=all)
- Dashboard Business View toggle
- Kalan (Özet Kartları + summary) → Phase 2'ye taşındı

### Flow Builder Phase 1~5 (12-14 Sub)
- **FB-1:** React 18 + Vite + React Flow + Zustand, 5 node component
- **FB-2:** JWT auth, CRUD, proxy, FlowListPage
- **FB-3:** FlowGraphV2 + FlowEngineV2 + FlowValidator + FlowMigrator (12 validation rule)
- **FB-4:** 7 yeni node handler (Logic, AI, Action, Utility) + SSRF koruması
- **FB-5:** SimulationPanel + keyboard shortcuts + validation UI + ghost path + FlowSummaryBar
- Ertelenen: iframe bridge, auto-save, tema (backlog)
- Planlar: `arch/plans/20260212-flow-builder-phase2.json` ~ `20260214-flow-builder-phase5.json`

### WA-1~3 WhatsApp Analytics Pipeline (14 Sub)
- WA-1: Temizlik + Threading (2.1M -> 164K konuşma)
- WA-2: NLP Pipeline (intent, FAQ, sentiment, product)
- WA-3: Training Data Export -> Knowledge DB
- Plan: `arch/plans/20260214-whatsapp-analytics.json`

### GR-2.1 Knowledge Service - Port 7104 (14-15 Sub)
- Phase A: RAG + pgvector (text-embedding-3-large, 3072 dim) + WA-3 import. Codex 5 iter PASS
- Phase B: PDF upload (PdfPig, 512-token/50-overlap) + Dashboard UI (FaqManager, DocumentUpload). Codex 3 iter PASS
- DB: documents, chunks, faqs, tags, document_tags
- Plan: `arch/plans/20260214-knowledge-service.json`

### WA-5/6 Phase A - Port 7109 (15 Sub)
- C# port of Python stages 1-3
- Streaming CSV, Turkish normalization, SHA256 dedup
- IHostedService background processing
- Codex 4 iter PASS. Commit: 18f387f
- Plan: `arch/plans/20260214-wa-analytics-phaseA.json`

## GR-1.4~1.8: Phase 2'ye Tasindi

| Eski GR | Yeni Yer | Açıklama |
|---------|----------|----------|
| GR-1.4 Otomasyon Dashboard | GR-2.5 (PKT-3) | Deflection rate, trend grafikleri |
| GR-1.5 Diş Pipeline | GR-3.9 (PKT-6A) | Fiyat→randevu intent |
| GR-1.6 Randevu Motoru | GR-2.4 (PKT-2) | Basit slot + hatırlatma |
| GR-1.7 Estetik Lead | GR-3.12 (PKT-6A) | Lead tracking |
| GR-1.8 KVKK | GR-2.6 (PKT-2) | Disclaimer, opt-in |

## Olusturulan Servisler

| Servis | Port | GR |
|--------|------|----|
| Backend | 5000 | Stage-0 |
| ChatAnalysis | 7101 | Stage-0 |
| Automation | 7108 | GR-1.1 |
| AgentAI | 7105 | GR-1.2 |
| Outbound | 7107 | GR-1.3 |
| Knowledge | 7104 | GR-2.1 |
| WhatsAppAnalytics | 7109 | WA-5/6 |
