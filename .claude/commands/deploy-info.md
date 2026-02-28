# InvektoServices Deploy Info

On-demand referans — deploy, server bilgileri ve SPA architecture detayları.

## Deploy Rules

- **Backend deploy = SPA build ZORUNLU:** Backend deploy edilmeden ÖNCE `npx vite build` Dashboard için çalıştırılmalı
- **Sıra:** (1) Dashboard vite build (output: wwwroot/app/), (2) dotnet publish, (3) server-deploy
- **FlowBuilder artık ayrı SPA DEĞİL** — Dashboard içine merge edildi (2026-02-22), tek build yeterli
- **Diğer servisler (Automation, Knowledge, vb.):** Sadece `dotnet publish` + `server-deploy` yeterli (SPA yok)

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
