@echo off
REM ============================================
REM  INVEKTO - Windows Firewall Rules (v2)
REM  New Server: C:\Invekto\
REM  Run as Administrator on production server
REM ============================================

echo.
echo Adding Invekto firewall rules...
echo.

REM =====================
REM  APPLICATION PORTS
REM =====================

REM Backend (port 5000) - External access (Main App webhooks + health checks)
netsh advfirewall firewall add rule name="Invekto Backend (TCP 5000)" dir=in action=allow protocol=tcp localport=5000 profile=any

REM ChatAnalysis (port 7101) - Localhost only (Backend calls internally)
netsh advfirewall firewall add rule name="Invekto ChatAnalysis (TCP 7101)" dir=in action=allow protocol=tcp localport=7101 profile=any remoteip=127.0.0.1

REM Appointments (port 7102) - Localhost only (Backend proxies)
netsh advfirewall firewall add rule name="Invekto Appointments (TCP 7102)" dir=in action=allow protocol=tcp localport=7102 profile=any remoteip=127.0.0.1

REM Knowledge (port 7104) - Localhost only (Backend proxies)
netsh advfirewall firewall add rule name="Invekto Knowledge (TCP 7104)" dir=in action=allow protocol=tcp localport=7104 profile=any remoteip=127.0.0.1

REM AgentAI (port 7105) - Localhost only (Backend proxies)
netsh advfirewall firewall add rule name="Invekto AgentAI (TCP 7105)" dir=in action=allow protocol=tcp localport=7105 profile=any remoteip=127.0.0.1

REM Integrations (port 7106) - Localhost only (Backend proxies)
netsh advfirewall firewall add rule name="Invekto Integrations (TCP 7106)" dir=in action=allow protocol=tcp localport=7106 profile=any remoteip=127.0.0.1

REM Outbound (port 7107) - Localhost only (Backend proxies)
netsh advfirewall firewall add rule name="Invekto Outbound (TCP 7107)" dir=in action=allow protocol=tcp localport=7107 profile=any remoteip=127.0.0.1

REM Automation (port 7108) - External access (Main App webhooks)
netsh advfirewall firewall add rule name="Invekto Automation (TCP 7108)" dir=in action=allow protocol=tcp localport=7108 profile=any

REM WhatsAppAnalytics (port 7109) - Localhost only (Backend proxies)
netsh advfirewall firewall add rule name="Invekto WhatsAppAnalytics (TCP 7109)" dir=in action=allow protocol=tcp localport=7109 profile=any remoteip=127.0.0.1

REM Marketing (port 7112) - Localhost only (Backend proxies)
netsh advfirewall firewall add rule name="Invekto Marketing (TCP 7112)" dir=in action=allow protocol=tcp localport=7112 profile=any remoteip=127.0.0.1

REM =====================
REM  DATABASE
REM =====================

REM PostgreSQL (port 5432) - External access (Dev PC MCP + tools)
netsh advfirewall firewall add rule name="Invekto PostgreSQL (TCP 5432)" dir=in action=allow protocol=tcp localport=5432 profile=any

REM =====================
REM  DEV TOOLS
REM =====================

REM Simulator (port 4500) - External access (Dev tool UI + callback receiver)
netsh advfirewall firewall add rule name="Invekto Simulator (TCP 4500)" dir=in action=allow protocol=tcp localport=4500 profile=any

REM SSH/OpenSSH (port 22) - External access (Dev PC MCP ops)
netsh advfirewall firewall add rule name="Invekto SSH (TCP 22)" dir=in action=allow protocol=tcp localport=22 profile=any

REM FTP/FTPES (ports 21, 990, 1024-1048 passive range)
netsh advfirewall firewall add rule name="Invekto FTP Control (TCP 21)" dir=in action=allow protocol=tcp localport=21 profile=any
netsh advfirewall firewall add rule name="Invekto FTPES (TCP 990)" dir=in action=allow protocol=tcp localport=990 profile=any
netsh advfirewall firewall add rule name="Invekto FTP Passive (TCP 1024-1048)" dir=in action=allow protocol=tcp localport=1024-1048 profile=any

echo.
echo ============================================
echo  Firewall rules added:
echo.
echo  --- APPLICATION ---
echo    5000  Backend         (external - webhooks)
echo    7101  ChatAnalysis    (localhost only)
echo    7102  Appointments    (localhost only)
echo    7104  Knowledge       (localhost only)
echo    7105  AgentAI         (localhost only)
echo    7106  Integrations    (localhost only)
echo    7107  Outbound        (localhost only)
echo    7108  Automation      (external - webhooks)
echo    7109  WA Analytics    (localhost only)
echo    7112  Marketing       (localhost only)
echo.
echo  --- DATABASE ---
echo    5432  PostgreSQL      (external - dev MCP)
echo.
echo  --- DEV TOOLS ---
echo    4500  Simulator       (external - dev UI)
echo      22  SSH             (external - MCP ops)
echo      21  FTP Control     (external - deploy)
echo     990  FTPES           (external - deploy)
echo    1024-1048  FTP Passive (external - deploy)
echo ============================================
echo.
pause
