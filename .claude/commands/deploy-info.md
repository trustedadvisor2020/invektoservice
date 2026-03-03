# InvektoServices Deploy Info

On-demand referans — deploy, server bilgileri ve SPA architecture detayları.

## Deploy Rules

- **Migration ÖNCE çalışır:** Deploy komutu verildiğinde, publish/deploy ÖNCE `arch/db/migrations/` altındaki bekleyen SQL dosyaları production DB'de çalıştırılır. Sıra: (1) Migration SQL çalıştır → (2) Publish → (3) Deploy. Bu kural tüm projelerde geçerlidir.
- **Backend deploy = SPA build ZORUNLU:** Backend deploy edilmeden ÖNCE `npx vite build` Dashboard için çalıştırılmalı
- **Sıra (Backend):** (1) Migration SQL, (2) Dashboard vite build (output: wwwroot/app/), (3) dotnet publish, (4) server-deploy
- **FlowBuilder artık ayrı SPA DEĞİL** — Dashboard içine merge edildi (2026-02-22), tek build yeterli
- **Diğer servisler (Automation, Knowledge, vb.):** (1) Migration SQL, (2) `dotnet publish` + `server-deploy` (SPA yok)
- **WebChat:** `dotnet publish` + manuel deploy (deploy tool enum'da henüz yok). Domain: `chat.invekto.com`

## SPA Architecture (2026-02-22)

- **Tek SPA:** Dashboard + FlowBuilder = tek React app, `/app/` altında serve edilir
- **Lazy loading:** FlowBuilder sayfaları `React.lazy()` ile code-split
- **Auth:** `exchangeInmaToken()` primary token'ı değiştirir (fb_session KALDIRILDI)
- **Backend:** `MapFallbackToFile("app/{*path:nonfile}", "app/index.html")` + root redirect `/app/`

## Server Details

| Alan | Değer |
|------|-------|
| NSSM path | `C:\Invekto\nssm.exe` (NOT E:\nssm.exe) |
| Service path | `C:\Invekto\{Service}\current\` (NOT E:\InvektoServices\) |
| Total services | 11 (Backend + 10 microservices) |
| WebChat domain | `chat.invekto.com` → `localhost:7113` (reverse proxy + WebSocket) |
| Restart | `Restart-Service <ServiceName> -Force` veya `C:\invekto\nssm.exe restart <ServiceName>` |
| PowerShell curl | Her zaman `curl.exe` kullan (explicit), yoksa Invoke-WebRequest alias olarak çalışır |

**Ops credentials:** HATIRLAMAYA çalışma, her zaman `C:\Invekto\Backend\current\appsettings.Production.json` Ops section'dan oku.

## Yeni Servis Deploy Checklist

Her yeni mikroservis oluşturulduğunda Q'ya OTOMATIK sun:
1. SQL schema FK/PK doğrula
2. appsettings.Production.json oluştur
3. install-services.bat güncelle
4. restart-services.bat güncelle
5. firewall-rules.bat güncelle
