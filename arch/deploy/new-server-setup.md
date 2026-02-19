# Invekto New Server Setup Guide

> Yeni sunucu kurulum rehberi. Adim adim takip et.
> Tarih: 2026-02-18 | Hedef: C:\Invekto\

---

## Onkoşullar (Zaten Tamamlanan)

- [x] Windows Server kurulu
- [x] PostgreSQL 18.2 kurulu ve calisiyor
- [x] Data eski sunucudan tasinmis (35+ tablo, pgvector dahil)
- [x] services.invekto.com DNS yeni sunucuya yonlu
- [x] PostgreSQL remote erisim acik (listen_addresses, pg_hba.conf)
- [x] Dev PC MCP baglantisi BASARILI (`.mcp.json` → services.invekto.com)
- [x] Firewall kurallari uygulanmis (firewall-rules-v2.bat)
- [x] .NET 8 ASP.NET Core Runtime 8.0.24 kurulu
- [x] NSSM `C:\nssm.exe` mevcut
- [x] Dizin yapisi olusturulmus (`C:\invekto\`)
- [x] ADIM 1-4 tamamlandi (FW, .NET, NSSM, dizinler)
- [x] FTP Server kurulu (FileZilla Server)

---

## ADIM 1: Firewall Kurallari

Admin CMD ac:

```cmd
C:\Invekto\scripts\firewall-rules-v2.bat
```

Veya script dosyasi henuz sunucuda degilse, dosyayi Dev PC'den kopyala:
`arch/deploy/firewall-rules-v2.bat` -> `C:\Invekto\scripts\firewall-rules-v2.bat`

---

## ADIM 2: .NET 8 ASP.NET Core Runtime Kur

Indir: https://dotnet.microsoft.com/en-us/download/dotnet/8.0

**"ASP.NET Core Runtime 8.0.x - Windows - x64 - Hosting Bundle"** indir ve kur.

Dogrula:
```powershell
dotnet --list-runtimes
```

`Microsoft.AspNetCore.App 8.0.x` gozukmeli.

---

## ADIM 3: NSSM (Non-Sucking Service Manager) Kur

1. https://nssm.cc/download adresinden son surumu indir
2. `nssm.exe`'yi `C:\nssm.exe` olarak kopyala

Dogrula:
```powershell
C:\nssm.exe version
```

---

## ADIM 4: Dizin Yapisi Olustur

Admin CMD:
```cmd
mkdir C:\Invekto
mkdir C:\Invekto\Backend\current
mkdir C:\Invekto\Backend\logs
mkdir C:\Invekto\ChatAnalysis\current
mkdir C:\Invekto\ChatAnalysis\logs
mkdir C:\Invekto\Automation\current
mkdir C:\Invekto\Automation\logs
mkdir C:\Invekto\AgentAI\current
mkdir C:\Invekto\AgentAI\logs
mkdir C:\Invekto\Outbound\current
mkdir C:\Invekto\Outbound\logs
mkdir C:\Invekto\Knowledge\current
mkdir C:\Invekto\Knowledge\logs
mkdir C:\Invekto\Appointments\current
mkdir C:\Invekto\Appointments\logs
mkdir C:\Invekto\Integrations\current
mkdir C:\Invekto\Integrations\logs
mkdir C:\Invekto\WhatsAppAnalytics\current
mkdir C:\Invekto\WhatsAppAnalytics\logs
mkdir C:\Invekto\WhatsAppAnalytics\uploads
mkdir C:\Invekto\Marketing\current
mkdir C:\Invekto\Marketing\logs
mkdir C:\Invekto\Simulator
mkdir C:\Invekto\scripts
mkdir C:\Invekto\logs
```

---

## ADIM 5: FTP Server Kurulumu (Deploy icin)

### Secenekler:

**A. Windows IIS FTP (Dahili):**
1. Server Manager -> Add Roles and Features -> Web Server (IIS) -> FTP Server
2. IIS Manager -> Sites -> Add FTP Site
   - Site name: InvektoFTP
   - Physical path: `C:\Invekto`
   - Binding: IP: All, Port: 21, SSL: Allow
   - Authentication: Basic
   - Authorization: Administrator -> Read/Write

**B. FileZilla Server (3rd Party):**
1. https://filezilla-project.org/download.php?type=server adresinden indir
2. Kur, kullanici olustur, `C:\Invekto` root olarak ayarla
3. Passive port range: 1024-1048

### FTP Dogrulama (Dev PC'den):
```powershell
# WinSCP test
& "C:\Program Files (x86)\WinSCP\WinSCP.com" /command "open ftpes://KULLANICI:SIFRE@services.invekto.com" "ls /c/Invekto/" "exit"
```

---

## ADIM 6: appsettings.Production.json Olustur

Her servis icin `C:\Invekto\{Servis}\current\appsettings.Production.json` dosyasi gerekli.

### Backend (C:\Invekto\Backend\current\appsettings.Production.json):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Jwt": {
    "SecretKey": "AYNI_JWT_SECRET_ESKI_SUNUCUDAN",
    "Issuer": "InvektoBackend",
    "Audience": "InvektoServices"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=invekto;Username=invekto;Password=32SZ0U421tSr5FgLqT19TE1;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=20"
  },
  "ServiceUrls": {
    "ChatAnalysis": "http://localhost:7101",
    "AgentAI": "http://localhost:7105",
    "Outbound": "http://localhost:7107",
    "Knowledge": "http://localhost:7104",
    "Appointments": "http://localhost:7102",
    "Automation": "http://localhost:7108",
    "Integrations": "http://localhost:7106",
    "WhatsAppAnalytics": "http://localhost:7109",
    "Marketing": "http://localhost:7112"
  },
  "Claude": {
    "ApiKey": "AYNI_CLAUDE_API_KEY_ESKI_SUNUCUDAN"
  }
}
```

### Diger Servisler (ChatAnalysis, Automation, AgentAI, Outbound, Knowledge, Appointments, Integrations, WhatsAppAnalytics, Marketing):

Her biri icin minimum config:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=invekto;Username=invekto;Password=32SZ0U421tSr5FgLqT19TE1;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=20"
  },
  "Claude": {
    "ApiKey": "AYNI_CLAUDE_API_KEY_ESKI_SUNUCUDAN"
  }
}
```

> **NOT:** Eski sunucudaki `appsettings.Production.json` dosyalarini direkt kopyalamak en guvenli yol. JWT secret, API key'ler hep ayni kalir.

---

## ADIM 7: Ilk Deploy (Dev PC'den)

Deploy script'i guncelledikten sonra (bkz. `dev-to-invekto-services.bat`):

1. Dev PC'de: `dev-to-invekto-services.bat` calistir
2. Build + Upload tamamlaninca devam

> **ONEMLI:** Deploy script henuz guncellenmedi (E:\ -> C:\ path'leri). Guncellemeden once Q onay vermeli.

---

## ADIM 8: NSSM Servisleri Kur

Deploy tamamlandiktan sonra sunucuda Admin CMD ile:

### 9a. Deploy Watcher Script Kopyala

`arch/deploy/deploy-watcher.ps1` dosyasini `C:\Invekto\scripts\deploy-watcher.ps1` olarak kopyala.

**DIKKAT:** deploy-watcher.ps1 icindeki path'ler `C:\Invekto\` olmali (guncellenmis versiyon).

### 9b. Servisleri Kur

Guncel `install-services.bat`'i sunucuda calistir (C:\ path'li versiyon):

```cmd
C:\Invekto\scripts\install-services.bat
```

### 9c. Deploy Watcher Kur

```cmd
C:\Invekto\scripts\install-deploy-watcher.bat
```

---

## ADIM 9: Dogrulama

### 10a. Servis Durumlari

```powershell
C:\nssm.exe status InvektoBackend
C:\nssm.exe status InvektoChatAnalysis
C:\nssm.exe status InvektoAutomation
C:\nssm.exe status InvektoAgentAI
C:\nssm.exe status InvektoOutbound
C:\nssm.exe status InvektoKnowledge
C:\nssm.exe status InvektoAppointments
C:\nssm.exe status InvektoIntegrations
C:\nssm.exe status InvektoWhatsAppAnalytics
C:\nssm.exe status InvektoMarketing
```

Hepsi `SERVICE_RUNNING` olmali.

### 10b. Health Check (Sunucu uzerinde)

```powershell
Invoke-RestMethod http://localhost:5000/health
Invoke-RestMethod http://localhost:7101/health
Invoke-RestMethod http://localhost:7102/health
Invoke-RestMethod http://localhost:7104/health
Invoke-RestMethod http://localhost:7105/health
Invoke-RestMethod http://localhost:7106/health
Invoke-RestMethod http://localhost:7107/health
Invoke-RestMethod http://localhost:7108/health
Invoke-RestMethod http://localhost:7109/health
Invoke-RestMethod http://localhost:7112/health
```

### 10c. External Health Check (Dev PC'den)

```powershell
Invoke-RestMethod http://services.invekto.com:5000/health
```

### 10d. PostgreSQL MCP Test (Dev PC'den)

Claude Code'da:
```
SELECT count(*) FROM tenants;
```

### 10e. Deploy Test (Dev PC'den)

`dev-to-invekto-services.bat` calistir ve basarili tamamlandigini dogrula.

---

## Sorun Giderme

### Servis baslamiyor (Error 1067)
```powershell
# Log'a bak
Get-Content C:\Invekto\Backend\logs\service-stderr.log -Tail 50
```

### PostgreSQL baglanti hatasi
```powershell
# PG dinliyor mu?
netstat -an | findstr 5432

# pg_hba.conf ve postgresql.conf kontrol
Get-Content "C:\Program Files\PostgreSQL\18\data\pg_hba.conf" | Select-String "invekto"
Get-Content "C:\Program Files\PostgreSQL\18\data\postgresql.conf" | Select-String "listen_addresses"
```

### Health check 404/hata
```powershell
# appsettings.Production.json var mi?
Test-Path C:\Invekto\Backend\current\appsettings.Production.json

# ASPNETCORE_ENVIRONMENT set mi?
C:\nssm.exe get InvektoBackend AppEnvironmentExtra
```

### FTP baglanti hatasi
```powershell
# Port 21 acik mi?
netstat -an | findstr ":21"

# Firewall kurali var mi?
netsh advfirewall firewall show rule name="Invekto FTP Control (TCP 21)"
```

---

## Port Haritasi (Referans)

| Port | Servis | Erisim |
|------|--------|--------|
| 22 | SSH (OpenSSH) | External (MCP ops) |
| 21 | FTP Control | External (deploy) |
| 990 | FTPES | External (deploy) |
| 1024-1048 | FTP Passive | External (deploy) |
| 4500 | Simulator | External (dev) |
| 5000 | Backend | External (webhooks) |
| 5432 | PostgreSQL | External (dev MCP) |
| 7101 | ChatAnalysis | Localhost |
| 7102 | Appointments | Localhost |
| 7104 | Knowledge | Localhost |
| 7105 | AgentAI | Localhost |
| 7106 | Integrations | Localhost |
| 7107 | Outbound | Localhost |
| 7108 | Automation | External (webhooks) |
| 7109 | WhatsAppAnalytics | Localhost |
| 7112 | Marketing | Localhost |
