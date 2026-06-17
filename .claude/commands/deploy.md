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

## Service List (12 services)

> **NSSM Name DASH KULLANMAZ.** Get-Service ile dogrulanmis (2026-04-15).
> **PORTLAR `server-health` ile DOGRULANDI (2026-06-11):** 5xxx serisi eski taslakti — gercek portlar asagida (10 servis health probe ciktisindan; WebChat/VoiceAI health enum'da yok, o ikisi dogrulanmadi). Smoke testte yanlis porta curl 000 doner (list-record deploy'unda yasandi: Outbound 5104 sanilip 7107 cikti).

| Service | Port (verified 2026-06-11) | NSSM Name | Path |
|---------|------|-----------|------|
| Backend | **5000** (+443 https) | InvektoBackend | `C:\Invekto\Backend\current\` |
| ChatAnalysis | 7101 | InvektoChatAnalysis | `C:\Invekto\ChatAnalysis\current\` |
| Appointments | 7102 | InvektoAppointments | `C:\Invekto\Appointments\current\` |
| Knowledge | 7104 | InvektoKnowledge | `C:\Invekto\Knowledge\current\` |
| AgentAI | 7105 | InvektoAgentAI | `C:\Invekto\AgentAI\current\` |
| Integrations | 7106 | InvektoIntegrations | `C:\Invekto\Integrations\current\` |
| Outbound | 7107 | InvektoOutbound | `C:\Invekto\Outbound\current\` |
| Automation | 7108 | InvektoAutomation | `C:\Invekto\Automation\current\` |
| WhatsAppAnalytics | 7109 | InvektoWhatsAppAnalytics | `C:\Invekto\WhatsAppAnalytics\current\` |
| Marketing | 7112 | InvektoMarketing | `C:\Invekto\Marketing\current\` |
| WebChat | ? (dogrulanmadi) | InvektoWebChat | `C:\Invekto\WebChat\current\` |
| VoiceAI | 7114 (dogrulanmadi) | InvektoVoiceAI | `C:\Invekto\VoiceAI\current\` |

## Deploy Steps (ORDER MATTERS)

1. **Migration ÖNCE:** `arch/db/migrations/` altındaki bekleyen SQL'leri production DB'de çalıştır
2. **(Backend special)** Dashboard SPA build: `cd src/Invekto.Backend/Dashboard && npm run build` (= `tsc && vite build`; vite.config `base:'/app/'` + `outDir:'../wwwroot/app'` → çıktı `wwwroot/app/`). FlowBuilder Dashboard'a merge edildi (2026-02-22), tek build yeterli. **Frontend-ONLY değişiklikte** (.cs YOK): dotnet publish/restart **GEREKMEZ** — `wwwroot/app`'i zip'le → `server-upload` → server-exec backup-swap extract (`Expand-Archive`→`app_new`, `Rename app→app_bak_<ts>`, `Rename app_new→app`) = **zero-downtime static swap** + anında rollback (`app_bak_<ts>`)
3. `dotnet publish -c Release` target service(s)
4. `mcp__invekto-ops__server-upload` → `C:\Invekto\{Service}\current\`
5. `mcp__invekto-ops__server-exec` → `C:\Invekto\nssm.exe restart Invekto{Service}` (DASHSIZ; NOT `Restart-Service` — sib process kill sorunu)
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