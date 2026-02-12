# Active Work Tracker

> Devam eden işler. Session başında kontrol et.

## In Progress

| Slug | Status | Started | Description |
|------|--------|---------|-------------|
| (none) | - | - | - |
| 20260212-flow-builder | PHASE1_DONE | 2026-02-12 | Flow Builder Phase 1 (SPA scaffold + canvas) tamamlandi, Phase 2 (API + Backend) sirada |

---

## Recently Completed

| Slug | Completed | Description |
|------|-----------|-------------|
| 20260212-outbound-service | 2026-02-12 | GR-1.3: Invekto.Outbound broadcast & trigger engine (Port 7107) - Codex 3 iter PASS |
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
