@echo off
title Install Deploy Watcher Task
color 0E

echo.
echo ============================================
echo    Install Deploy Watcher (Scheduled Task)
echo ============================================
echo.
echo This creates a scheduled task that watches for
echo deploy flag files and stops/starts services.
echo.

REM Check admin
net session >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Run as Administrator!
    pause
    exit /b 1
)

set "SCRIPT_PATH=D:\Invekto\scripts\deploy-watcher.ps1"
set "TASK_NAME=Invekto.DeployWatcher"

REM Check script exists
if not exist "%SCRIPT_PATH%" (
    echo [ERROR] Script not found: %SCRIPT_PATH%
    echo Please copy deploy-watcher.ps1 to D:\Invekto\scripts\
    pause
    exit /b 1
)

echo [1/3] Removing existing task if any...
schtasks /delete /tn "%TASK_NAME%" /f 2>nul

echo [2/3] Creating scheduled task...
schtasks /create ^
    /tn "%TASK_NAME%" ^
    /tr "powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File \"%SCRIPT_PATH%\"" ^
    /sc onstart ^
    /ru SYSTEM ^
    /rl HIGHEST ^
    /f

if errorlevel 1 (
    echo [ERROR] Failed to create task!
    pause
    exit /b 1
)

echo [3/3] Starting task now...
schtasks /run /tn "%TASK_NAME%"

echo.
color 0A
echo ============================================
echo    Deploy Watcher Installed!
echo ============================================
echo.
echo Task: %TASK_NAME%
echo Script: %SCRIPT_PATH%
echo Log: D:\Invekto\logs\deploy-watcher.log
echo.
echo The watcher will:
echo   - Start automatically on server boot
echo   - Watch for deploy-stop.flag (stops services)
echo   - Watch for deploy-start.flag (starts services)
echo.
echo To check status: schtasks /query /tn "%TASK_NAME%"
echo To view logs: type D:\Invekto\logs\deploy-watcher.log
echo.
pause
