# Session Memory

> Son session'dan kalan context. Her session başında oku.

## Last Update

- **Date:** 2026-02-02
- **Status:** Stage-0 Implementation Complete
- **Last Task:** Backend + ChatAnalysis microservice scaffold

---

## Current State

### Active Features
- **Stage-0 Scaffold:** Backend + ChatAnalysis microservice çalışır durumda
- **Health Endpoints:** `/health`, `/ready` her iki serviste
- **Ops Endpoint:** Backend `/ops` - servis durumlarını gösterir
- **JSON Lines Logger:** `Invekto.Shared.Logging.JsonLinesLogger`

### Tech Stack
| Component | Technology |
|-----------|------------|
| Backend | .NET 8 Minimal API |
| Microservice | .NET 8 Minimal API + Windows Service |
| Shared | .NET 8 Class Library |
| Logging | JSON Lines (custom) |

### Ports
| Service | Port |
|---------|------|
| Backend | 5000 |
| ChatAnalysis | 7101 |

### Pending Work
- [ ] Chat Analysis gerçek iş mantığı
- [ ] Ops sayfası genişletme (son 100 hata, son 100 yavaş)
- [ ] Windows Service olarak deploy testi
- [ ] Production config (D:\Invekto\ yapısı)

### Known Issues
- (Henüz yok)

---

## Recent Decisions

| Date | Decision | Reason |
|------|----------|--------|
| 2026-02-01 | Mikro servis mimarisi | Bağımsız deploy, ölçeklenebilirlik |
| 2026-02-02 | .NET 8 stack | Windows Service native, backend ile aynı ekosistem |
| 2026-02-02 | Stage-0 önce | Full system yerine hızlı MVP |

---

## Project Structure

```
src/
├── Invekto.Shared/           # Shared contracts, DTOs, logging
│   ├── Constants/
│   ├── DTOs/
│   └── Logging/
├── Invekto.ChatAnalysis/     # Microservice (Port 7101)
└── Invekto.Backend/          # Backend API (Port 5000)
    └── Services/
```

---

## Context for Next Session

Sonraki session'da:
1. Chat Analysis servisine gerçek iş mantığı eklenecek
2. Ops sayfası genişletilecek (log okuma)
3. Windows Service deploy testi yapılacak

---

## Notes

- Stage-0 dokümanı: `invekto_stage0_kurulum_adimlari.txt`
- Full system dokümanı: `invekto_microservice_system_plan.txt`
- Error codes: `arch/errors.md` ve `Invekto.Shared/Constants/ErrorCodes.cs`
