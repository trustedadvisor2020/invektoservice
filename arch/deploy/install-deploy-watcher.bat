@echo off
REM ============================================
REM  INVEKTO - Deploy Watcher Service Installation
REM  Run ONCE as Administrator on server
REM  Watches for deploy flag files (FTP-based deploy)
REM ============================================

set "NSSM=E:\nssm.exe"
set "SCRIPT=E:\Invekto\scripts\deploy-watcher.ps1"

echo.
echo ============================================
echo  Installing Deploy Watcher Service
echo ============================================
echo.

REM Create scripts directory
if not exist "E:\Invekto\scripts" mkdir "E:\Invekto\scripts"
if not exist "E:\Invekto\logs" mkdir "E:\Invekto\logs"

REM Copy watcher script (run this after copying deploy-watcher.ps1 to E:\Invekto\scripts\)
if not exist "%SCRIPT%" (
    echo [ERROR] Watcher script not found: %SCRIPT%
    echo Copy deploy-watcher.ps1 to E:\Invekto\scripts\ first!
    goto :end
)

echo Installing InvektoDeployWatcher service...
%NSSM% install InvektoDeployWatcher "powershell.exe" "-ExecutionPolicy Bypass -NoProfile -File \"%SCRIPT%\""
%NSSM% set InvektoDeployWatcher DisplayName "Invekto Deploy Watcher"
%NSSM% set InvektoDeployWatcher Description "Watches for deploy flag files and restarts Invekto services"
%NSSM% set InvektoDeployWatcher AppDirectory "E:\Invekto"
%NSSM% set InvektoDeployWatcher AppStdout "E:\Invekto\logs\watcher-stdout.log"
%NSSM% set InvektoDeployWatcher AppStderr "E:\Invekto\logs\watcher-stderr.log"
%NSSM% set InvektoDeployWatcher AppStdoutCreationDisposition 4
%NSSM% set InvektoDeployWatcher AppStderrCreationDisposition 4
%NSSM% set InvektoDeployWatcher AppRotateFiles 1
%NSSM% set InvektoDeployWatcher AppRotateBytes 5242880
%NSSM% set InvektoDeployWatcher Start SERVICE_AUTO_START
%NSSM% set InvektoDeployWatcher AppExit Default Restart
%NSSM% set InvektoDeployWatcher AppRestartDelay 3000

echo.
echo Starting watcher...
%NSSM% start InvektoDeployWatcher

echo.
echo ============================================
echo  Deploy Watcher Installed!
echo ============================================
echo.
echo Service: InvektoDeployWatcher
%NSSM% status InvektoDeployWatcher
echo.
echo How it works:
echo   1. Deploy script sends deploy-stop.flag via FTP
echo   2. Watcher detects flag, stops Backend + ChatAnalysis
echo   3. Deploy script uploads new files
echo   4. Deploy script sends deploy-start.flag via FTP
echo   5. Watcher detects flag, starts services
echo.
echo Log: E:\Invekto\logs\deploy-watcher.log
echo.

:end
pause
