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
   .\dev-to-staging.bat

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
GUNCELLEME (HER DEPLOY)
============================================

DEV PC'de (build + FTP upload otomatik):
   .\dev-to-staging.bat

SUNUCUDA (servisleri restart):
   D:\Invekto\scripts\restart-services.bat


============================================
DIZIN YAPISI
============================================

D:\Invekto\
  nssm.exe             <- NSSM service manager
  Backend\
    current\           <- binary'ler + appsettings.Production.json
    logs\
      stdout.log       <- console output (NSSM rotates)
      stderr.log       <- errors (NSSM rotates)
      *.jsonl          <- JSON structured logs
  ChatAnalysis\
    current\           <- binary'ler + appsettings.Production.json
    logs\
      stdout.log
      stderr.log
      *.jsonl
  scripts\
    install-services.bat
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

install-services.bat   - Ilk kurulum (Admin gerekli)
restart-services.bat   - Deploy sonrasi restart
status-services.bat    - Durum + health check
uninstall-services.bat - Servisleri kaldir


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
