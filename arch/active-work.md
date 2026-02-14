# Active Work Tracker

> Devam eden işler. Session başında kontrol et.

## In Progress

| Slug | Status | Started | Description |
|------|--------|---------|-------------|
| (none) | - | - | - |

---

## Execution Queue (Onaylı Sıra)

> **Kural:** WA = WhatsApp Analytics, RP = Roadmap Phase. Karışmasın!

| Sıra | Kod | İş | Bağımlılık | Notlar |
|------|-----|----|------------|--------|
| 1 | **WA-3 + RP-2 GR-2.1** | Training Data Export + Knowledge Service (RAG) | WA-2 ✅ | FAQ clusters → Knowledge DB. BERABER yapılacak, ayrı yapmak 2x iş |
| 2 | **WA-4** | BI Dashboard (agent performans, conversion, trend) | WA-2 ✅ | Bağımsız, arada yapılabilir |
| 3 | **RP-2 GR-2.2** | Agent Assist v2 (Knowledge/RAG beslemeli) | RP-2 GR-2.1 | Knowledge Service SONRASI |
| 4 | **RP-2 GR-2.3~2.6** | Multi-lang, Randevu, Dashboard, KVKK | RP-2 GR-2.1 | Sırayla veya paralel |
| 5 | **WA-5 + WA-6** | C# Microservice + SQL Server Entegrasyon | RP-2 GR-2.1 | Aynı infra pattern, en son |

### WA (WhatsApp Analytics) Fazları

| Faz | İsim | Durum | Açıklama |
|-----|------|-------|----------|
| WA-1 | Temizlik + Threading | ✅ 2026-02-14 | 01_cleaner, 02_threader, 03_stats |
| WA-2 | NLP Pipeline | ✅ 2026-02-14 | 04_intent, 05_faq, 06_sentiment, 07_product + shared claude_client |
| WA-3 | Training Data Export | ⬜ Sırada | FAQ clusters + intent patterns → Knowledge Service'e aktar |
| WA-4 | BI Dashboard | ⬜ Sırada | Agent performans, conversion, trend raporları |
| WA-5 | C# Microservice | ⬜ Bekliyor | Pipeline'ı InvektoServices mikro servis olarak sarmala |
| WA-6 | SQL Server Entegrasyon | ⬜ Bekliyor | CSV'lerden DB'ye bulk insert, live query |

---

## Recently Completed

| Slug | Completed | Description |
|------|-----------|-------------|
| 20260214-whatsapp-analytics | 2026-02-14 | WA-2: NLP Pipeline (intent classifier, FAQ extractor, sentiment analyzer, product analyzer + shared claude_client). 8 dosya +1919. Codex 3 iter PASS. Plan: `arch/plans/20260214-whatsapp-analytics.json` |
| 20260214-idea-phase-integration | 2026-02-14 | 5 idea dokümanı roadmap phase'lerine entegre edildi (v4.5): Voice AI→3B GR-3.23, Face Analysis→3D GR-3D.1-5, Size/Fit→3C GR-3C.8, Review Rescue→3B GR-3.24, Multilingual→3B GR-3.25. Yeni phase-3d.md oluşturuldu. |
| 20260214-flow-builder-phase5 | 2026-02-14 | Flow Builder Phase 5: Production Integration. Deploy script'e FlowBuilder SPA build adimi eklendi. 1 dosya +26 -5. Codex 3 iter Q FORCE PASS. |
| 20260214-flow-builder-phase4b | 2026-02-14 | Flow Builder Phase 4b: AI/API Nodes (ai_intent, ai_faq, action_api_call). 3 handler + IntentDetector refactor + SSRF validation + 3 SPA node + 3 property editor + graph-validator + flow-summarizer. 27 dosya +1516 -104. Codex 4 iter PASS. |
| 20260214-flow-builder-phase4a | 2026-02-14 | Flow Builder Phase 4a: Pure Logic Nodes (logic_condition, logic_switch, action_delay, utility_set_variable). 4 handler + 4 SPA node + property editors + validation. 25 dosya +964 -42. Codex 3 iter Q FORCE PASS. |
| 20260213-flow-builder-phase3c | 2026-02-14 | Flow Builder Phase 3c: Validation UI + Variable Inspector + AHA #3 Ghost Path + AHA #5 Health Score. 20 dosya +746 -195. Codex 3 iter PASS. |
| 20260213-flow-builder-phase3b | 2026-02-13 | Flow Builder Phase 3b: SimulationEngine + SPA Chat Panel + AHA #4 Tek Tikla Test. 25 dosya +1461 -199. Codex 3 iter PASS. |
| 20260213-flow-builder-phase3a | 2026-02-13 | Flow Builder Phase 3a: FlowEngine v2 + Validator + Migrator + 5 NodeHandlers. 16 dosya +1942 -27. Codex 3 iter Q FORCE PASS. |
| 20260213-flow-builder-phase25 | 2026-02-13 | Flow Builder Phase 2.5 SPA Quick Wins - AHA #6 Kopya, #2 Kirmizi Kenar, #1 Canli Onizleme. Codex 2 iter PASS. |
| 20260212-flow-builder | 2026-02-13 | Flow Builder Phase 2 (API + Backend + Multi-flow + Auth) - Codex 3 iter, Q FORCE PASS. Committed + deployed. |
| 20260212-outbound-service | 2026-02-12 | GR-1.3: Invekto.Outbound broadcast & trigger engine (Port 7107) - Codex 3 iter PASS. Deployed (NSSM). |
| 20260211-testing-tooling | 2026-02-11 | Backend proxy architecture for simulator + E2E scenarios - Codex 5 iter PASS |
| 20260211-agentai-service | 2026-02-11 | GR-1.11: Invekto.AgentAI AI agent assist (Port 7105) - Codex 2 iter, Q FORCE PASS |
| 20260209-simulator | 2026-02-09 | Test & Simulation tool (Node.js, Port 4500) - Codex 3 iter PASS |
| 20260209-automation-service | 2026-02-09 | GR-1.1: Invekto.Automation chatbot/flow builder servisi (Codex 3 iter, Q FORCE PASS) |
| 20260208-integration-bridge | 2026-02-08 | GR-1.9: Invekto <-> InvektoServis API koprusu (JWT, webhook, callback, PostgreSQL) |
| 20260202-chatanalysis-integration | 2026-02-02 | WapCRM + Claude Haiku integration |
| 20260202-stage0-review-fixes | 2026-02-02 | Ops auth + log reader fixes |
| 20260202-stage0-scaffold | 2026-02-02 | Stage-0 scaffold: Backend + ChatAnalysis + Shared |
| 20260201-initial-setup | 2026-02-01 | Proje workflow yapısı kuruldu |

---

## Blocked

| Slug | Blocked Since | Reason | Waiting For |
|------|---------------|--------|-------------|
| (none) | - | - | - |

---

## Stage-0 Checklist

| Item | Status |
|------|--------|
| Solution yapısı | ✅ |
| Invekto.Shared | ✅ |
| Invekto.ChatAnalysis | ✅ |
| Invekto.Backend | ✅ |
| /health endpoint | ✅ |
| /ops endpoint | ✅ |
| JSON Lines logger | ✅ |
| 600ms timeout | ✅ |
| 0 retry | ✅ |
| Windows Service ready | ✅ |
| Build PASS | ✅ |
| WapCRM integration | ✅ |
| Claude Haiku analysis | ✅ |
| Sentiment/Category API | ✅ |

---

## Usage

### Yeni İş Başlatma
```markdown
| {slug} | IN_PROGRESS | {tarih} | {açıklama} |
```

### İş Tamamlama
1. In Progress'ten kaldır
2. Recently Completed'a ekle

### İş Engellenirse
1. In Progress'ten Blocked'a taşı
2. Waiting For alanını doldur
