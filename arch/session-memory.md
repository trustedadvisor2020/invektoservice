# Session Memory

> Claude'un session arası context taşıması için. Her session sonunda güncellenir.

## Workflow v1.0 (ACTIVE)

**Otomatik Copy-Paste Workflow:**
- DevAgent ↔ Codex ayrı pencere
- Q Interview: Gri nokta kalmayana kadar sor
- MEDIUM+ risk: Verification Questions zorunlu
- Codex: 2 BLOK output (Code Quality + CoVe)
- Max 3 iter → kategorize escalation

**Risk-Based Trigger:**
| Risk | Build PASS Sonrası |
|------|-------------------|
| LOW | Codex review |
| MEDIUM+ | Codex review |
| CRITICAL | Codex + Q onay |

**Referans Dosyalar:**
- `.claude/commands/auto.md` - Workflow
- `.claude/commands/rev.md` - Review protokolü
- `CLAUDE.md` - Ana konfigürasyon
- `agents.md` - Agent kuralları

## Last Session
- **Date:** 2026-01-29
- **Task:** Phase 0: Foundation & Contracts
- **Status:** COMPLETED
- **Summary:** Solution structure, Invekto.Contracts, Invekto.Infrastructure oluşturuldu. GitHub Actions CI eklendi.

## Where We Left Off
- **Current Step:** Phase 0 complete
- **Next Action:** Phase 1: Infrastructure Setup (Redis, RabbitMQ, Nginx)
- **Blockers:** None

## Recent Decisions
| Date | Decision | Reason |
|------|----------|--------|
| 2026-01-29 | IaC: PowerShell scripts | Windows-only, basit, yeterli |
| 2026-01-29 | No inter-service auth | Internal network güvenli |
| 2026-01-29 | Env variables for secrets | Encrypted config gereksiz |

## Open Questions
- [x] Backend API detayları? → HTTP REST API, JSON
- [x] İlk microservice: Chat Analysis → Evet
- [x] Package strategy → Project Reference
- [x] CI/CD platform → GitHub Actions

## Notes for Next Session
- **Master Plan:** `plans/00_master_implementation_plan.md`
- **Phase 0:** Solution structure, shared contracts
- **Tech stack:** .NET 8.0, Redis, RabbitMQ, Nginx
