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
echo    Target: services.invekto.com
echo ============================================
echo.

REM ============================================
REM Configuration
REM ============================================
set "WINSCP_PATH=C:\Program Files (x86)\WinSCP\WinSCP.com"
set "FTP_HOST=services.invekto.com"
set "FTP_USER=arkin-2024.i2025"
set "FTP_PASS=O2SZ0U4so5tSr5FgLqT19TE"
set "REMOTE_BACKEND=/e/Invekto/Backend/current"
set "REMOTE_CHATANALYSIS=/e/Invekto/ChatAnalysis/current"
set "REMOTE_AUTOMATION=/e/Invekto/Automation/current"
set "REMOTE_AGENTAI=/e/Invekto/AgentAI/current"
set "REMOTE_OUTBOUND=/e/Invekto/Outbound/current"
set "REMOTE_SIMULATOR=/e/Invekto/Simulator"

REM Local paths
set "SRC_DIR=%~dp0"
set "LOCAL_DEPLOY=%~dp0deploy_output"
set "LOCAL_BACKEND=%LOCAL_DEPLOY%\Backend"
set "LOCAL_CHATANALYSIS=%LOCAL_DEPLOY%\ChatAnalysis"
set "LOCAL_AUTOMATION=%LOCAL_DEPLOY%\Automation"
set "LOCAL_AGENTAI=%LOCAL_DEPLOY%\AgentAI"
set "LOCAL_OUTBOUND=%LOCAL_DEPLOY%\Outbound"
set "LOCAL_SIMULATOR=%~dp0tools\simulator"

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
if not exist "!LOCAL_AUTOMATION!" mkdir "!LOCAL_AUTOMATION!"
if not exist "!LOCAL_AGENTAI!" mkdir "!LOCAL_AGENTAI!"
if not exist "!LOCAL_OUTBOUND!" mkdir "!LOCAL_OUTBOUND!"

echo ============================================
echo [1/6] Building FlowBuilder SPA...
echo ============================================
echo.

cd /d "%SRC_DIR%src\Invekto.Backend\FlowBuilder"
call npm ci
if errorlevel 1 (
    echo [ERROR] FlowBuilder npm ci failed!
    goto :error_exit
)
call npm run build
if errorlevel 1 (
    echo [ERROR] FlowBuilder SPA build failed!
    goto :error_exit
)

REM Verify build output exists
if not exist "%SRC_DIR%src\Invekto.Backend\wwwroot\flow-builder\index.html" (
    echo [ERROR] FlowBuilder SPA output not found!
    echo Expected: src\Invekto.Backend\wwwroot\flow-builder\index.html
    goto :error_exit
)
echo [OK] FlowBuilder SPA built to wwwroot\flow-builder\
echo.

echo ============================================
echo [2/6] Building Backend...
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
echo [3/6] Building ChatAnalysis...
echo ============================================
echo.

dotnet publish src/Invekto.ChatAnalysis/Invekto.ChatAnalysis.csproj -c Release -o "!LOCAL_CHATANALYSIS!" --self-contained false
if errorlevel 1 (
    echo [ERROR] ChatAnalysis build failed!
    goto :error_exit
)
echo [OK] ChatAnalysis built to !LOCAL_CHATANALYSIS!
echo.

echo ============================================
echo [4/6] Building Automation...
echo ============================================
echo.

dotnet publish src/Invekto.Automation/Invekto.Automation.csproj -c Release -o "!LOCAL_AUTOMATION!" --self-contained false
if errorlevel 1 (
    echo [ERROR] Automation build failed!
    goto :error_exit
)
echo [OK] Automation built to !LOCAL_AUTOMATION!
echo.

echo ============================================
echo [5/6] Building AgentAI...
echo ============================================
echo.

dotnet publish src/Invekto.AgentAI/Invekto.AgentAI.csproj -c Release -o "!LOCAL_AGENTAI!" --self-contained false
if errorlevel 1 (
    echo [ERROR] AgentAI build failed!
    goto :error_exit
)
echo [OK] AgentAI built to !LOCAL_AGENTAI!
echo.

echo ============================================
echo [6/6] Building Outbound...
echo ============================================
echo.

dotnet publish src/Invekto.Outbound/Invekto.Outbound.csproj -c Release -o "!LOCAL_OUTBOUND!" --self-contained false
if errorlevel 1 (
    echo [ERROR] Outbound build failed!
    goto :error_exit
)
echo [OK] Outbound built to !LOCAL_OUTBOUND!
echo.

REM Q: Create build marker
for /f "tokens=*" %%i in ('git rev-parse --short HEAD 2^>nul') do set "GIT_HASH=%%i"
for /f "tokens=*" %%i in ('git rev-parse --abbrev-ref HEAD 2^>nul') do set "GIT_BRANCH=%%i"
powershell -NoProfile -Command "$marker = @{ timestamp = (Get-Date).ToString('o'); gitHash = '%GIT_HASH%'; gitBranch = '%GIT_BRANCH%'; services = @('FlowBuilder','Backend','ChatAnalysis','Automation','AgentAI','Outbound') }; [System.IO.File]::WriteAllText('!LOCAL_DEPLOY!\.build-marker.json', ($marker | ConvertTo-Json))"
echo [OK] Build marker created (%GIT_BRANCH%@%GIT_HASH%)
echo.

echo ============================================
echo [7/9] Stopping Remote Services...
echo ============================================
echo.
echo Creating deploy-stop.flag to trigger service shutdown...

REM Create local flag file
echo deploy > "%TEMP%\deploy-stop.flag"

set "WINSCP_STOP=%TEMP%\winscp_stop.txt"
(
    echo option batch abort
    echo option confirm off
    echo open ftpes://%FTP_HOST% -username="%FTP_USER%" -password="%FTP_PASS%" -passive -timeout=30 -certificate=*
    echo put "%TEMP%\deploy-stop.flag" "/e/Invekto/"
    echo exit
) > "!WINSCP_STOP!"

"!WINSCP_PATH!" /script="!WINSCP_STOP!" /log="%TEMP%\winscp_stop.log"
del "!WINSCP_STOP!" 2>nul
del "%TEMP%\deploy-stop.flag" 2>nul

echo [OK] Stop flag sent
echo Waiting 10 seconds for watcher to stop services...
timeout /t 10 /nobreak >nul

echo.
echo ============================================
echo [8/9] Uploading to STAGING Server...
echo ============================================
echo.
echo Local Backend:      !LOCAL_BACKEND!
echo Local ChatAnalysis: !LOCAL_CHATANALYSIS!
echo Local Automation:   !LOCAL_AUTOMATION!
echo Local AgentAI:      !LOCAL_AGENTAI!
echo Local Outbound:     !LOCAL_OUTBOUND!
echo Remote Backend:     %REMOTE_BACKEND%
echo Remote ChatAnalysis: %REMOTE_CHATANALYSIS%
echo Remote Automation:  %REMOTE_AUTOMATION%
echo Remote AgentAI:     %REMOTE_AGENTAI%
echo Remote Outbound:    %REMOTE_OUTBOUND%
echo.
echo Mode: Synchronize (only changed files)

set "WINSCP_SCRIPT=%TEMP%\winscp_invekto_deploy.txt"
(
    echo option batch continue
    echo option confirm off
    echo option reconnecttime 5
    echo open ftpes://%FTP_HOST% -username="%FTP_USER%" -password="%FTP_PASS%" -passive -timeout=30 -certificate=*
    echo option batch continue
    echo option reconnecttime 5
    echo echo Ensuring remote directories exist...
    echo mkdir "%REMOTE_BACKEND%"
    echo mkdir "%REMOTE_CHATANALYSIS%"
    echo mkdir "%REMOTE_AUTOMATION%"
    echo mkdir "%REMOTE_AGENTAI%"
    echo mkdir "%REMOTE_OUTBOUND%"
    echo mkdir "%REMOTE_SIMULATOR%"
    echo option batch abort
    echo echo Uploading Backend to STAGING...
    echo synchronize remote "!LOCAL_BACKEND!" "%REMOTE_BACKEND%" -mirror -transfer=binary -criteria=size,time -resumesupport=on -filemask="|appsettings.Production.json"
    echo echo Uploading ChatAnalysis to STAGING...
    echo synchronize remote "!LOCAL_CHATANALYSIS!" "%REMOTE_CHATANALYSIS%" -mirror -transfer=binary -criteria=size,time -resumesupport=on -filemask="|appsettings.Production.json"
    echo echo Uploading Automation to STAGING...
    echo synchronize remote "!LOCAL_AUTOMATION!" "%REMOTE_AUTOMATION%" -mirror -transfer=binary -criteria=size,time -resumesupport=on -filemask="|appsettings.Production.json"
    echo echo Uploading AgentAI to STAGING...
    echo synchronize remote "!LOCAL_AGENTAI!" "%REMOTE_AGENTAI%" -mirror -transfer=binary -criteria=size,time -resumesupport=on -filemask="|appsettings.Production.json"
    echo echo Uploading Outbound to STAGING...
    echo synchronize remote "!LOCAL_OUTBOUND!" "%REMOTE_OUTBOUND%" -mirror -transfer=binary -criteria=size,time -resumesupport=on -filemask="|appsettings.Production.json"
    echo echo Uploading Simulator to STAGING...
    echo synchronize remote "!LOCAL_SIMULATOR!" "%REMOTE_SIMULATOR%" -mirror -transfer=binary -criteria=size,time -resumesupport=on -filemask="|node_modules/;.env"
    echo echo Uploading build marker...
    echo put "!LOCAL_DEPLOY!\.build-marker.json" "/e/Invekto/"
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

echo.
echo ============================================
echo [9/9] Starting Remote Services...
echo ============================================
echo.
echo Creating deploy-start.flag to trigger service startup...

REM Create local flag file
echo deploy > "%TEMP%\deploy-start.flag"

set "WINSCP_START=%TEMP%\winscp_start.txt"
(
    echo option batch abort
    echo option confirm off
    echo open ftpes://%FTP_HOST% -username="%FTP_USER%" -password="%FTP_PASS%" -passive -timeout=30 -certificate=*
    echo put "%TEMP%\deploy-start.flag" "/e/Invekto/"
    echo exit
) > "!WINSCP_START!"

"!WINSCP_PATH!" /script="!WINSCP_START!" /log="%TEMP%\winscp_start.log"
del "!WINSCP_START!" 2>nul
del "%TEMP%\deploy-start.flag" 2>nul

echo [OK] Start flag sent - services will restart automatically

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
echo   Automation:   %REMOTE_AUTOMATION%
echo   AgentAI:      %REMOTE_AGENTAI%
echo   Outbound:     %REMOTE_OUTBOUND%
echo   Simulator:    %REMOTE_SIMULATOR%
echo.
echo ============================================
echo NEXT STEPS (on STAGING server):
echo ============================================
echo   1. Copy appsettings.Production.json to each current folder
echo   2. Run: E:\Invekto\scripts\restart-services.bat
echo   3. Test: http://services.invekto.com:5000/health
echo.
echo First time? Run: E:\Invekto\scripts\install-services.bat
echo.
echo Simulator (first time on server):
echo   cd E:\Invekto\Simulator ^&^& npm install
echo   Create .env with SIMULATOR_JWT_SECRET
echo   node server.js (or install as NSSM service)
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
