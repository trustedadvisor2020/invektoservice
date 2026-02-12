@echo off
REM ============================================
REM  INVEKTO - Windows Service Installation (NSSM)
REM  Run as Administrator on server
REM ============================================

set "NSSM=E:\nssm.exe"

echo.
echo ============================================
echo  Installing Invekto Windows Services (NSSM)
echo ============================================
echo.

REM Backend Service
echo [1/5] Installing InvektoBackend...
%NSSM% install InvektoBackend "E:\Invekto\Backend\current\Invekto.Backend.exe"
%NSSM% set InvektoBackend DisplayName "Invekto Backend"
%NSSM% set InvektoBackend Description "Invekto Backend API - Port 5000"
%NSSM% set InvektoBackend AppDirectory "E:\Invekto\Backend\current"
%NSSM% set InvektoBackend AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
%NSSM% set InvektoBackend AppStdout "E:\Invekto\Backend\logs\service-stdout.log"
%NSSM% set InvektoBackend AppStderr "E:\Invekto\Backend\logs\service-stderr.log"
%NSSM% set InvektoBackend AppStdoutCreationDisposition 4
%NSSM% set InvektoBackend AppStderrCreationDisposition 4
%NSSM% set InvektoBackend AppRotateFiles 1
%NSSM% set InvektoBackend AppRotateBytes 10485760
%NSSM% set InvektoBackend Start SERVICE_AUTO_START
%NSSM% set InvektoBackend AppExit Default Restart
%NSSM% set InvektoBackend AppRestartDelay 5000
echo [OK] InvektoBackend installed
echo.

REM ChatAnalysis Service
echo [2/5] Installing InvektoChatAnalysis...
%NSSM% install InvektoChatAnalysis "E:\Invekto\ChatAnalysis\current\Invekto.ChatAnalysis.exe"
%NSSM% set InvektoChatAnalysis DisplayName "Invekto ChatAnalysis"
%NSSM% set InvektoChatAnalysis Description "Invekto Chat Analysis Microservice - Port 7101"
%NSSM% set InvektoChatAnalysis AppDirectory "E:\Invekto\ChatAnalysis\current"
%NSSM% set InvektoChatAnalysis AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
%NSSM% set InvektoChatAnalysis AppStdout "E:\Invekto\ChatAnalysis\logs\service-stdout.log"
%NSSM% set InvektoChatAnalysis AppStderr "E:\Invekto\ChatAnalysis\logs\service-stderr.log"
%NSSM% set InvektoChatAnalysis AppStdoutCreationDisposition 4
%NSSM% set InvektoChatAnalysis AppStderrCreationDisposition 4
%NSSM% set InvektoChatAnalysis AppRotateFiles 1
%NSSM% set InvektoChatAnalysis AppRotateBytes 10485760
%NSSM% set InvektoChatAnalysis Start SERVICE_AUTO_START
%NSSM% set InvektoChatAnalysis AppExit Default Restart
%NSSM% set InvektoChatAnalysis AppRestartDelay 5000
echo [OK] InvektoChatAnalysis installed
echo.

REM Automation Service
echo [3/5] Installing InvektoAutomation...
%NSSM% install InvektoAutomation "E:\Invekto\Automation\current\Invekto.Automation.exe"
%NSSM% set InvektoAutomation DisplayName "Invekto Automation"
%NSSM% set InvektoAutomation Description "Invekto Automation Chatbot/Flow Builder - Port 7108"
%NSSM% set InvektoAutomation AppDirectory "E:\Invekto\Automation\current"
%NSSM% set InvektoAutomation AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
%NSSM% set InvektoAutomation AppStdout "E:\Invekto\Automation\logs\service-stdout.log"
%NSSM% set InvektoAutomation AppStderr "E:\Invekto\Automation\logs\service-stderr.log"
%NSSM% set InvektoAutomation AppStdoutCreationDisposition 4
%NSSM% set InvektoAutomation AppStderrCreationDisposition 4
%NSSM% set InvektoAutomation AppRotateFiles 1
%NSSM% set InvektoAutomation AppRotateBytes 10485760
%NSSM% set InvektoAutomation Start SERVICE_AUTO_START
%NSSM% set InvektoAutomation AppExit Default Restart
%NSSM% set InvektoAutomation AppRestartDelay 5000
echo [OK] InvektoAutomation installed
echo.

REM AgentAI Service
echo [4/5] Installing InvektoAgentAI...
%NSSM% install InvektoAgentAI "E:\Invekto\AgentAI\current\Invekto.AgentAI.exe"
%NSSM% set InvektoAgentAI DisplayName "Invekto AgentAI"
%NSSM% set InvektoAgentAI Description "Invekto AI Agent Assist Microservice - Port 7105"
%NSSM% set InvektoAgentAI AppDirectory "E:\Invekto\AgentAI\current"
%NSSM% set InvektoAgentAI AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
%NSSM% set InvektoAgentAI AppStdout "E:\Invekto\AgentAI\logs\service-stdout.log"
%NSSM% set InvektoAgentAI AppStderr "E:\Invekto\AgentAI\logs\service-stderr.log"
%NSSM% set InvektoAgentAI AppStdoutCreationDisposition 4
%NSSM% set InvektoAgentAI AppStderrCreationDisposition 4
%NSSM% set InvektoAgentAI AppRotateFiles 1
%NSSM% set InvektoAgentAI AppRotateBytes 10485760
%NSSM% set InvektoAgentAI Start SERVICE_AUTO_START
%NSSM% set InvektoAgentAI AppExit Default Restart
%NSSM% set InvektoAgentAI AppRestartDelay 5000
echo [OK] InvektoAgentAI installed
echo.

REM Outbound Service
echo [5/5] Installing InvektoOutbound...
%NSSM% install InvektoOutbound "E:\Invekto\Outbound\current\Invekto.Outbound.exe"
%NSSM% set InvektoOutbound DisplayName "Invekto Outbound"
%NSSM% set InvektoOutbound Description "Invekto Outbound Broadcast Messaging - Port 7107"
%NSSM% set InvektoOutbound AppDirectory "E:\Invekto\Outbound\current"
%NSSM% set InvektoOutbound AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
%NSSM% set InvektoOutbound AppStdout "E:\Invekto\Outbound\logs\service-stdout.log"
%NSSM% set InvektoOutbound AppStderr "E:\Invekto\Outbound\logs\service-stderr.log"
%NSSM% set InvektoOutbound AppStdoutCreationDisposition 4
%NSSM% set InvektoOutbound AppStderrCreationDisposition 4
%NSSM% set InvektoOutbound AppRotateFiles 1
%NSSM% set InvektoOutbound AppRotateBytes 10485760
%NSSM% set InvektoOutbound Start SERVICE_AUTO_START
%NSSM% set InvektoOutbound AppExit Default Restart
%NSSM% set InvektoOutbound AppRestartDelay 5000
echo [OK] InvektoOutbound installed
echo.

REM Create log directories
if not exist "E:\Invekto\Backend\logs" mkdir "E:\Invekto\Backend\logs"
if not exist "E:\Invekto\ChatAnalysis\logs" mkdir "E:\Invekto\ChatAnalysis\logs"
if not exist "E:\Invekto\Automation\logs" mkdir "E:\Invekto\Automation\logs"
if not exist "E:\Invekto\AgentAI\logs" mkdir "E:\Invekto\AgentAI\logs"
if not exist "E:\Invekto\Outbound\logs" mkdir "E:\Invekto\Outbound\logs"

REM Start services
echo Starting services...
%NSSM% start InvektoBackend
timeout /t 3 /nobreak >nul
%NSSM% start InvektoChatAnalysis
timeout /t 3 /nobreak >nul
%NSSM% start InvektoAutomation
timeout /t 3 /nobreak >nul
%NSSM% start InvektoAgentAI
timeout /t 3 /nobreak >nul
%NSSM% start InvektoOutbound
timeout /t 3 /nobreak >nul

echo.
echo ============================================
echo  Installation Complete!
echo ============================================
echo.
echo Services:
%NSSM% status InvektoBackend
%NSSM% status InvektoChatAnalysis
%NSSM% status InvektoAutomation
%NSSM% status InvektoAgentAI
%NSSM% status InvektoOutbound
echo.
echo Test:
echo   http://localhost:5000/health
echo   http://localhost:7101/health
echo   http://localhost:7105/health
echo   http://localhost:7107/health
echo   http://localhost:7108/health
echo.
echo Manage:
echo   %NSSM% edit InvektoBackend
echo   %NSSM% edit InvektoChatAnalysis
echo   %NSSM% edit InvektoAutomation
echo   %NSSM% edit InvektoAgentAI
echo   %NSSM% edit InvektoOutbound
echo.
pause
