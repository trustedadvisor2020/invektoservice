@echo off
title Invekto - Restart Services (NSSM)
color 0E

echo.
echo ============================================
echo    Invekto - Restart Services (NSSM)
echo ============================================
echo.

set "NSSM=D:\Invekto\nssm.exe"

if not exist "%NSSM%" (
    echo [ERROR] NSSM not found at: %NSSM%
    pause
    exit /b 1
)

echo [1/4] Stopping Backend...
"%NSSM%" stop Invekto.Backend
timeout /t 2 /nobreak >nul

echo [2/4] Stopping ChatAnalysis...
"%NSSM%" stop Invekto.ChatAnalysis
timeout /t 2 /nobreak >nul

echo [3/4] Starting ChatAnalysis...
"%NSSM%" start Invekto.ChatAnalysis
timeout /t 5 /nobreak >nul

echo [4/4] Starting Backend...
"%NSSM%" start Invekto.Backend
timeout /t 3 /nobreak >nul

echo.
echo ============================================
echo    Health Check
echo ============================================
echo.

echo ChatAnalysis status:
"%NSSM%" status Invekto.ChatAnalysis
echo.

echo Backend status:
"%NSSM%" status Invekto.Backend
echo.

echo.
echo HTTP Health:
curl -s http://127.0.0.1:7101/health
echo.
curl -s http://127.0.0.1:5000/health
echo.

echo.
color 0A
echo ============================================
echo    Services Restarted!
echo ============================================
echo.
pause
