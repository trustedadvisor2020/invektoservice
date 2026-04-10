---
description: "InvektoServices deploy - migration + build + publish + restart (production only)"
---

# /deploy [service]

> InvektoServices production deploy. **Sadece production** — dev deploy yoktur.
> Detaylı teknik referans: `deploy-info.md` (aynı klasörde).

## Usage

```
/deploy              # Full deploy (all 11 services)
/deploy backend      # Only Backend (+ Dashboard SPA build)
/deploy webchat      # Only WebChat
/deploy automation   # Only Automation
/deploy outbound     # Only Outbound
/deploy agentai      # Only AgentAI
/deploy knowledge    # Only Knowledge
/deploy whatsapp     # Only WhatsAppAnalytics
```

## Architecture (TRUTH — 2026-02-22 SPA merge + C:\Invekto reality)

```
Dev PC (build)                    Production Server
C:\CRMs\InvektoServices    ->     C:\Invekto\{Service}\current\
                                  NSSM Windows Services
                                  nssm path: C:\Invekto\nssm.exe
```

## Service List (11 services)

| Service | Port | NSSM Name | Path |
|---------|------|-----------|------|
| Backend | 5100 | Invekto-Backend | `C:\Invekto\Backend\current\` |
| WebChat | 5101 | Invekto-WebChat | `C:\Invekto\WebChat\current\` |
| Automation | 5102 | Invekto-Automation | `C:\Invekto\Automation\current\` |
| AgentAI | 5103 | Invekto-AgentAI | `C:\Invekto\AgentAI\current\` |
| Outbound | 5104 | Invekto-Outbound | `C:\Invekto\Outbound\current\` |
| Knowledge | 5105 | Invekto-Knowledge | `C:\Invekto\Knowledge\current\` |
| WhatsAppAnalytics | 5106 | Invekto-WhatsAppAnalytics | `C:\Invekto\WhatsAppAnalytics\current\` |
| VoiceAI | 7114 | Invekto-VoiceAI | `C:\Invekto\VoiceAI\current\` |
| Marketing | — | Invekto-Marketing | `C:\Invekto\Marketing\current\` |
| Translate | — | Invekto-Translate | `C:\Invekto\Translate\current\` |
| +1 | — | — | (detay: `deploy-info.md`) |

## Deploy Steps (ORDER MATTERS)

1. **Migration ÖNCE:** `arch/db/migrations/` altındaki bekleyen SQL'leri production DB'de çalıştır
2. **(Backend special)** Dashboard SPA build: `cd src/Invekto.Backend/wwwroot && npx vite build` (output: `wwwroot/app/`). FlowBuilder Dashboard'a merge edildi (2026-02-22), tek build yeterli
3. `dotnet publish -c Release` target service(s)
4. `mcp__invekto-ops__server-upload` → `C:\Invekto\{Service}\current\`
5. `mcp__invekto-ops__server-exec` → `C:\Invekto\nssm.exe restart Invekto-{Service}` (NOT `Restart-Service` — sib process kill sorunu)
6. `mcp__invekto-ops__server-health` ile doğrula

## Critical Rules

- **Sadece production.** `C:\CRMs\InvektoServices` = dev PC, `C:\Invekto\` = production. `E:\InvektoServices\` **YOKTUR** (eski taslak).
- **Migration-first:** Sıra (1) migration SQL → (2) Dashboard build (backend için) → (3) publish → (4) upload → (5) restart → (6) health
- **Shared DTO değişikliği:** Tüm etkilenen servisleri birlikte deploy et
- **Running process lock:** .NET DLL'ini lock'lar — `nssm stop` → copy → `nssm start` sırası
- **curl.exe:** PowerShell'de explicit (Invoke-WebRequest alias'ı değil)
- **Otonom self-healing YOK:** Bilinmeyen hata → DUR, Q'ya sor. Bilinen retry: 2x aynı build hatası → STOP

## Error Handling

| Hata | Aksiyon |
|------|---------|
| `dotnet publish` FAIL | Fix et, tekrar dene (max 2) |
| Upload FAIL | Retry 1x, fail → Q'ya rapor |
| NSSM restart FAIL | `nssm status` ile kontrol, log oku |
| Health check FAIL | `server-logs` ile incele, Q'ya rapor |

## Referanslar

- **Teknik detaylar:** `deploy-info.md` (aynı klasör, reference-only)
- **Migration kuralı:** `shared-lessons.md` (all projects)
- **Review:** `arch/review-policy.md` — LOW dahil tüm risk seviyeleri Codex review zorunlu