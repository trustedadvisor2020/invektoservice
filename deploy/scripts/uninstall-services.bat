@echo off
title Invekto - Uninstall Services (NSSM)
color 0C

echo.
echo ============================================
echo    Invekto - Uninstall Services (NSSM)
echo ============================================
echo.
echo WARNING: This will remove all Invekto services!
echo.

REM Check admin
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Please run as Administrator!
    pause
    exit /b 1
)

set "NSSM=D:\Invekto\nssm.exe"

if not exist "%NSSM%" (
    echo [ERROR] NSSM not found at: %NSSM%
    pause
    exit /b 1
)

set /p CONFIRM="Are you sure? (yes/no): "
if /i not "%CONFIRM%"=="yes" (
    echo Cancelled.
    pause
    exit /b 0
)

echo.
echo [1/4] Stopping Backend...
"%NSSM%" stop Invekto.Backend 2>nul
timeout /t 2 /nobreak >nul

echo [2/4] Stopping ChatAnalysis...
"%NSSM%" stop Invekto.ChatAnalysis 2>nul
timeout /t 2 /nobreak >nul

echo [3/4] Removing Backend service...
"%NSSM%" remove Invekto.Backend confirm

echo [4/4] Removing ChatAnalysis service...
"%NSSM%" remove Invekto.ChatAnalysis confirm

echo.
color 0A
echo ============================================
echo    Services Removed
echo ============================================
echo.
echo Note: Files in D:\Invekto\ were NOT deleted.
echo.
pause
