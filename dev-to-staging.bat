@echo off
setlocal enabledelayedexpansion
title INVEKTO - DEV to STAGING Build + Deploy
color 0E

REM Q: Record start time for duration measurement
set "START_TIME=%time%"
for /f "tokens=1-4 delims=:,. " %%a in ("%START_TIME%") do (
    set /a "START_S=(((%%a*60)+1%%b %% 100)*60+1%%c %% 100)"
)

echo.
echo ============================================
echo    INVEKTO - DEV to STAGING Deploy
echo    Target: 213.142.143.202
echo ============================================
echo.

REM ============================================
REM Configuration
REM ============================================
set "WINSCP_PATH=C:\Program Files (x86)\WinSCP\WinSCP.com"
set "FTP_HOST=213.142.143.202"
set "FTP_USER=arkin-2024.i2025"
set "FTP_PASS=O2SZ0U4so5tSr5FgLqT19TE"
set "REMOTE_BACKEND=/Backend/current"
set "REMOTE_CHATANALYSIS=/ChatAnalysis/current"

REM Local paths
set "SRC_DIR=%~dp0"
set "LOCAL_DEPLOY=%~dp0deploy_output"
set "LOCAL_BACKEND=%LOCAL_DEPLOY%\Backend"
set "LOCAL_CHATANALYSIS=%LOCAL_DEPLOY%\ChatAnalysis"

REM Check WinSCP exists
if not exist "!WINSCP_PATH!" (
    echo [ERROR] WinSCP not found at: !WINSCP_PATH!
    echo Please install WinSCP or update path
    goto :error_exit
)

REM Create local deploy folders
if not exist "!LOCAL_DEPLOY!" mkdir "!LOCAL_DEPLOY!"
if not exist "!LOCAL_BACKEND!" mkdir "!LOCAL_BACKEND!"
if not exist "!LOCAL_CHATANALYSIS!" mkdir "!LOCAL_CHATANALYSIS!"

echo ============================================
echo [1/3] Building Backend...
echo ============================================
echo.

cd /d "%SRC_DIR%"
dotnet publish src/Invekto.Backend/Invekto.Backend.csproj -c Release -o "!LOCAL_BACKEND!" --self-contained false
if errorlevel 1 (
    echo [ERROR] Backend build failed!
    goto :error_exit
)
echo [OK] Backend built to !LOCAL_BACKEND!
echo.

echo ============================================
echo [2/3] Building ChatAnalysis...
echo ============================================
echo.

dotnet publish src/Invekto.ChatAnalysis/Invekto.ChatAnalysis.csproj -c Release -o "!LOCAL_CHATANALYSIS!" --self-contained false
if errorlevel 1 (
    echo [ERROR] ChatAnalysis build failed!
    goto :error_exit
)
echo [OK] ChatAnalysis built to !LOCAL_CHATANALYSIS!
echo.

REM Q: Create build marker
for /f "tokens=*" %%i in ('git rev-parse --short HEAD 2^>nul') do set "GIT_HASH=%%i"
for /f "tokens=*" %%i in ('git rev-parse --abbrev-ref HEAD 2^>nul') do set "GIT_BRANCH=%%i"
powershell -NoProfile -Command "$marker = @{ timestamp = (Get-Date).ToString('o'); gitHash = '%GIT_HASH%'; gitBranch = '%GIT_BRANCH%'; services = @('Backend','ChatAnalysis') }; [System.IO.File]::WriteAllText('!LOCAL_DEPLOY!\.build-marker.json', ($marker | ConvertTo-Json))"
echo [OK] Build marker created (%GIT_BRANCH%@%GIT_HASH%)
echo.

echo ============================================
echo [3/3] Uploading to STAGING Server...
echo ============================================
echo.
echo Local Backend:      !LOCAL_BACKEND!
echo Local ChatAnalysis: !LOCAL_CHATANALYSIS!
echo Remote Backend:     %REMOTE_BACKEND%
echo Remote ChatAnalysis: %REMOTE_CHATANALYSIS%
echo.
echo Mode: Synchronize (only changed files)

set "WINSCP_SCRIPT=%TEMP%\winscp_invekto_deploy.txt"
(
    echo option batch continue
    echo option confirm off
    echo option reconnecttime 5
    echo open ftp://%FTP_HOST% -username="%FTP_USER%" -password="%FTP_PASS%" -passive -timeout=30
    echo option batch abort
    echo option reconnecttime 5
    echo echo Uploading Backend to STAGING...
    echo synchronize remote "!LOCAL_BACKEND!" "%REMOTE_BACKEND%" -mirror -transfer=binary -criteria=size,time -resumesupport=on
    echo echo Uploading ChatAnalysis to STAGING...
    echo synchronize remote "!LOCAL_CHATANALYSIS!" "%REMOTE_CHATANALYSIS%" -mirror -transfer=binary -criteria=size,time -resumesupport=on
    echo echo Uploading build marker...
    echo put "!LOCAL_DEPLOY!\.build-marker.json" "/"
    echo exit
) > "!WINSCP_SCRIPT!"

"!WINSCP_PATH!" /script="!WINSCP_SCRIPT!" /log="%TEMP%\winscp_invekto_deploy.log"
if errorlevel 1 (
    echo [ERROR] FTP upload failed!
    echo Check log: %TEMP%\winscp_invekto_deploy.log
    echo.
    echo TIP: If authentication fails, verify FTP credentials
    del "!WINSCP_SCRIPT!" 2>nul
    goto :error_exit
)
del "!WINSCP_SCRIPT!" 2>nul
echo [OK] FTP upload completed

REM Q: Calculate duration
set "END_TIME=%time%"
for /f "tokens=1-4 delims=:,. " %%a in ("%END_TIME%") do (
    set /a "END_S=(((%%a*60)+1%%b %% 100)*60+1%%c %% 100)"
)
set /a "DURATION=END_S-START_S"
if %DURATION% lss 0 set /a "DURATION+=86400"
set /a "DURATION_M=DURATION/60"
set /a "DURATION_S=DURATION%%60"

color 0A
echo.
echo ============================================
echo    STAGING DEPLOYMENT COMPLETED!
echo    Duration: %DURATION_M%m %DURATION_S%s
echo    Git: %GIT_BRANCH%@%GIT_HASH%
echo ============================================
echo.
echo Local: !LOCAL_DEPLOY!
echo.
echo Deployed to: %FTP_HOST%
echo   Backend:      %REMOTE_BACKEND%
echo   ChatAnalysis: %REMOTE_CHATANALYSIS%
echo.
echo ============================================
echo NEXT STEPS (on STAGING server):
echo ============================================
echo   1. Copy appsettings.Production.json to both current folders
echo   2. Run: D:\Invekto\scripts\restart-services.bat
echo   3. Test: http://213.142.143.202:5000/health
echo.
echo First time? Run: D:\Invekto\scripts\install-services.bat
echo.
endlocal
exit /b 0

:error_exit
color 0C
echo.
echo ============================================
echo    BUILD FAILED!
echo ============================================
echo.
pause
endlocal
exit /b 1
