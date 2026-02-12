@echo off
REM ============================================
REM  INVEKTO - Windows Firewall Rules
REM  Run as Administrator on STAGING server
REM ============================================

echo.
echo Adding Invekto firewall rules...
echo.

REM Backend (port 5000) - External access (Main App webhooks + health checks)
netsh advfirewall firewall add rule name="Invekto Backend (TCP 5000)" dir=in action=allow protocol=tcp localport=5000 profile=any

REM ChatAnalysis (port 7101) - Localhost only (Backend calls internally)
netsh advfirewall firewall add rule name="Invekto ChatAnalysis (TCP 7101)" dir=in action=allow protocol=tcp localport=7101 profile=any remoteip=127.0.0.1

REM AgentAI (port 7105) - Localhost only (Backend proxies, not direct external access)
netsh advfirewall firewall add rule name="Invekto AgentAI (TCP 7105)" dir=in action=allow protocol=tcp localport=7105 profile=any remoteip=127.0.0.1

REM Automation (port 7108) - External access (Main App webhooks)
netsh advfirewall firewall add rule name="Invekto Automation (TCP 7108)" dir=in action=allow protocol=tcp localport=7108 profile=any

REM Simulator (port 4500) - External access (Dev tool UI + callback receiver)
netsh advfirewall firewall add rule name="Invekto Simulator (TCP 4500)" dir=in action=allow protocol=tcp localport=4500 profile=any

REM PostgreSQL (port 5432) - Localhost only
netsh advfirewall firewall add rule name="Invekto PostgreSQL (TCP 5432)" dir=in action=allow protocol=tcp localport=5432 profile=any remoteip=127.0.0.1

echo.
echo ============================================
echo  Firewall rules added:
echo    4500  Simulator     (external)
echo    5000  Backend       (external)
echo    7101  ChatAnalysis  (localhost only)
echo    7105  AgentAI       (localhost only)
echo    7108  Automation    (external)
echo    5432  PostgreSQL    (localhost only)
echo ============================================
echo.
pause
