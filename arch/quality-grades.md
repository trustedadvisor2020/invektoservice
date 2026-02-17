# Service Quality Grades

> Son guncelleme: 2026-02-17
> Bu dosya her paket sonunda guncellenir.

## Grading Scale

| Grade | Anlam |
|-------|-------|
| A | Production-ready, tum kurallar saglanmis |
| B | Calisiyor, minor iyilestirme gereken alanlar var |
| C | Fonksiyonel ama teknik borc birikimis |
| D | Kritik sorunlar var, oncelikli refactoring gerek |
| F | Kullanilabilir degil |

## Service Grades

| Servis | Code Quality | DB Schema | Error Handling | Isolation | Test Coverage | Overall | Notlar |
|--------|-------------|-----------|----------------|-----------|---------------|---------|--------|
| Backend | B | B | B | A | D | B | Ana API gateway, stabil |
| Automation | B | B | B | A | D | B | Chatbot flows |
| AgentAI | B | B | C | A | D | B- | OpenAI integration |
| Knowledge | B | B | C | A | D | B- | RAG pipeline |
| Outbound | B | B | B | A | D | B | Broadcast/templates |
| WhatsAppAnalytics | C | B | C | A | D | C+ | NLP pipeline, yeni |
| Shared | B | - | B | - | D | B | DTOs, utilities |

## Tracking History

| Tarih | Degisiklik | Etkilenen Servis |
|-------|-----------|------------------|
| 2026-02-17 | Initial grading | Tumu |

## Oncelikli Iyilestirme Alanlari

1. **Test coverage (tum servisler D):** Unit test altyapisi kurulmali
2. **WhatsAppAnalytics error handling (C):** Standardize edilmeli
3. **AgentAI/Knowledge error handling (C):** INV-xxx kodlarina gecis
