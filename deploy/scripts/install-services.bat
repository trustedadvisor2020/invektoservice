@echo off
title Invekto - Install Services (NSSM)
color 0E

echo.
echo ============================================
echo    Invekto - Install Services (NSSM)
echo ============================================
echo.
echo This script must be run as Administrator!
echo.

REM Check admin
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Please run as Administrator!
    pause
    exit /b 1
)

REM NSSM path
set "NSSM=D:\Invekto\nssm.exe"

if not exist "%NSSM%" (
    echo [ERROR] NSSM not found at: %NSSM%
    pause
    exit /b 1
)

echo ============================================
echo [1/5] Creating directory structure...
echo ============================================

mkdir D:\Invekto\Backend\current 2>nul
mkdir D:\Invekto\Backend\logs 2>nul
mkdir D:\Invekto\ChatAnalysis\current 2>nul
mkdir D:\Invekto\ChatAnalysis\logs 2>nul

echo [OK] Directories created

echo.
echo ============================================
echo [2/5] Installing ChatAnalysis service...
echo ============================================

"%NSSM%" install Invekto.ChatAnalysis "D:\Invekto\ChatAnalysis\current\Invekto.ChatAnalysis.exe"
"%NSSM%" set Invekto.ChatAnalysis DisplayName "Invekto Chat Analysis"
"%NSSM%" set Invekto.ChatAnalysis Description "Invekto Chat Analysis Microservice - WapCRM + Claude AI"
"%NSSM%" set Invekto.ChatAnalysis AppDirectory "D:\Invekto\ChatAnalysis\current"
"%NSSM%" set Invekto.ChatAnalysis AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
"%NSSM%" set Invekto.ChatAnalysis AppStdout "D:\Invekto\ChatAnalysis\logs\stdout.log"
"%NSSM%" set Invekto.ChatAnalysis AppStderr "D:\Invekto\ChatAnalysis\logs\stderr.log"
"%NSSM%" set Invekto.ChatAnalysis AppRotateFiles 1
"%NSSM%" set Invekto.ChatAnalysis AppRotateBytes 10485760
"%NSSM%" set Invekto.ChatAnalysis Start SERVICE_AUTO_START

echo [OK] Invekto.ChatAnalysis installed

echo.
echo ============================================
echo [3/5] Installing Backend service...
echo ============================================

"%NSSM%" install Invekto.Backend "D:\Invekto\Backend\current\Invekto.Backend.exe"
"%NSSM%" set Invekto.Backend DisplayName "Invekto Backend"
"%NSSM%" set Invekto.Backend Description "Invekto Backend API - Main gateway service"
"%NSSM%" set Invekto.Backend AppDirectory "D:\Invekto\Backend\current"
"%NSSM%" set Invekto.Backend AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
"%NSSM%" set Invekto.Backend AppStdout "D:\Invekto\Backend\logs\stdout.log"
"%NSSM%" set Invekto.Backend AppStderr "D:\Invekto\Backend\logs\stderr.log"
"%NSSM%" set Invekto.Backend AppRotateFiles 1
"%NSSM%" set Invekto.Backend AppRotateBytes 10485760
"%NSSM%" set Invekto.Backend Start SERVICE_AUTO_START

echo [OK] Invekto.Backend installed

echo.
echo ============================================
echo [4/5] Setting dependencies...
echo ============================================

REM Backend depends on ChatAnalysis
"%NSSM%" set Invekto.Backend DependOnService Invekto.ChatAnalysis

echo [OK] Backend depends on ChatAnalysis

echo.
echo ============================================
echo [5/5] Starting services...
echo ============================================

"%NSSM%" start Invekto.ChatAnalysis
timeout /t 5 /nobreak >nul
"%NSSM%" start Invekto.Backend
timeout /t 3 /nobreak >nul

echo.
color 0A
echo ============================================
echo    INSTALLATION COMPLETE!
echo ============================================
echo.
echo Services installed:
echo   - Invekto.ChatAnalysis (Port 7101)
echo   - Invekto.Backend (Port 5000)
echo.
echo Check status:
echo   %NSSM% status Invekto.ChatAnalysis
echo   %NSSM% status Invekto.Backend
echo.
echo Edit service:
echo   %NSSM% edit Invekto.ChatAnalysis
echo.
pause
