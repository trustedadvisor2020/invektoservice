@echo off
title Invekto - Service Status
color 0B

set "NSSM=D:\Invekto\nssm.exe"

echo.
echo ============================================
echo    Invekto - Service Status
echo ============================================
echo.

echo ChatAnalysis:
"%NSSM%" status Invekto.ChatAnalysis
echo.

echo Backend:
"%NSSM%" status Invekto.Backend
echo.

echo ============================================
echo    HTTP Health Check
echo ============================================
echo.

echo ChatAnalysis (7101):
curl -s -w " [HTTP %%{http_code}]" http://127.0.0.1:7101/health
echo.
echo.

echo Backend (5000):
curl -s -w " [HTTP %%{http_code}]" http://127.0.0.1:5000/health
echo.
echo.

echo ============================================
echo    Recent Logs
echo ============================================
echo.

echo ChatAnalysis stderr (last 5 lines):
if exist "D:\Invekto\ChatAnalysis\logs\stderr.log" (
    powershell -NoProfile -Command "Get-Content 'D:\Invekto\ChatAnalysis\logs\stderr.log' -Tail 5"
) else (
    echo (no stderr log)
)
echo.

echo Backend stderr (last 5 lines):
if exist "D:\Invekto\Backend\logs\stderr.log" (
    powershell -NoProfile -Command "Get-Content 'D:\Invekto\Backend\logs\stderr.log' -Tail 5"
) else (
    echo (no stderr log)
)
echo.

pause
