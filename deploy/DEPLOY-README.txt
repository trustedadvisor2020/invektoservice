INVEKTO SUNUCU KURULUM REHBERI (NSSM)
=====================================

SUNUCU: 213.142.143.202
GEREKSINIMLER:
  - .NET 8 Runtime (ASP.NET Core Hosting Bundle)
  - NSSM (Non-Sucking Service Manager) - zaten D:\Invekto\nssm\ icinde


============================================
ILK KURULUM (TEK SEFERLIK)
============================================

1. .NET 8 Runtime kur:
   https://dotnet.microsoft.com/download/dotnet/8.0
   "ASP.NET Core Runtime 8.x - Hosting Bundle" indir ve kur

2. DEV PC'de calistir (binary'leri FTP ile yukler):
   .\dev-to-invekto-services.bat

3. Sunucuya MANUEL kopyala (sadece ilk seferde):
   deploy\scripts\*.bat              -> D:\Invekto\scripts\
   deploy\config\Backend\*           -> D:\Invekto\Backend\current\
   deploy\config\ChatAnalysis\*      -> D:\Invekto\ChatAnalysis\current\

4. Sunucuda API key'leri duzenle:
   - D:\Invekto\Backend\current\appsettings.Production.json
     - Ops.Password degistir

   - D:\Invekto\ChatAnalysis\current\appsettings.Production.json
     - WapCrm.SecretKey ekle
     - Claude.ApiKey ekle

5. Sunucuda Admin olarak calistir:
   D:\Invekto\scripts\install-services.bat

6. Test:
   http://127.0.0.1:5000/health
   http://127.0.0.1:7101/health


============================================
DEPLOY WATCHER KURULUMU (TEK SEFERLIK)
============================================

Deploy watcher, FTP uzerinden otomatik servis restart saglar.
SSH gerekmez - flag dosyalari ile calisir.

1. Scripti kopyala:
   deploy\scripts\deploy-watcher.ps1 -> D:\Invekto\scripts\

2. Sunucuda Admin olarak calistir:
   D:\Invekto\scripts\install-deploy-watcher.bat

3. Kontrol:
   schtasks /query /tn "Invekto.DeployWatcher"
   type D:\Invekto\logs\deploy-watcher.log


============================================
GUNCELLEME (HER DEPLOY) - OTOMATIK
============================================

DEV PC'de (build + FTP upload + auto restart):
   .\dev-to-invekto-services.bat

Bat dosyasi otomatik olarak:
  1. deploy-stop.flag gonderir -> Servisler durur
  2. 10 saniye bekler
  3. Dosyalari yukler
  4. deploy-start.flag gonderir -> Servisler baslar

MANUEL restart gerekirse (sunucuda):
   D:\Invekto\scripts\restart-services.bat


============================================
DIZIN YAPISI
============================================

D:\Invekto\
  nssm.exe                   <- NSSM service manager
  deploy-stop.flag           <- (gecici) servis durdurma tetikleyici
  deploy-start.flag          <- (gecici) servis baslatma tetikleyici
  Backend\
    current\                 <- binary'ler + appsettings.Production.json
    logs\
      stdout.log             <- console output (NSSM rotates)
      stderr.log             <- errors (NSSM rotates)
      *.jsonl                <- JSON structured logs
  ChatAnalysis\
    current\                 <- binary'ler + appsettings.Production.json
    logs\
      stdout.log
      stderr.log
      *.jsonl
  logs\
    deploy-watcher.log       <- deploy watcher loglari
  scripts\
    install-services.bat
    install-deploy-watcher.bat
    deploy-watcher.ps1
    restart-services.bat
    status-services.bat
    uninstall-services.bat


============================================
NSSM KOMUTLARI
============================================

# Durum kontrol
D:\Invekto\nssm.exe status Invekto.ChatAnalysis
D:\Invekto\nssm.exe status Invekto.Backend

# Durdur / Baslat
D:\Invekto\nssm.exe stop Invekto.ChatAnalysis
D:\Invekto\nssm.exe start Invekto.ChatAnalysis

# GUI ile ayar degistir
D:\Invekto\nssm.exe edit Invekto.ChatAnalysis

# Log izle
Get-Content D:\Invekto\ChatAnalysis\logs\stderr.log -Tail 50 -Wait


============================================
SCRIPTLER
============================================

install-services.bat        - Ilk kurulum (Admin gerekli)
install-deploy-watcher.bat  - Deploy watcher kurulumu (Admin)
restart-services.bat        - Manuel restart
status-services.bat         - Durum + health check
uninstall-services.bat      - Servisleri kaldir
deploy-watcher.ps1          - Flag izleme scripti (scheduled task)


============================================
PORTLAR
============================================

Backend:      5000  (dis dunyaya acik olabilir)
ChatAnalysis: 7101  (sadece localhost - DIS DUNYAYA ACMA!)


============================================
FIREWALL
============================================

# Backend'i disa ac (gerekirse)
New-NetFirewallRule -DisplayName "Invekto Backend" -Direction Inbound -Port 5000 -Protocol TCP -Action Allow

# ChatAnalysis'i ACMA! Sadece localhost erisir.
