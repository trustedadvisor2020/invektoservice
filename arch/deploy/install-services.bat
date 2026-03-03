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
echo [1/9] Installing InvektoBackend...
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
echo [2/9] Installing InvektoChatAnalysis...
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
echo [3/9] Installing InvektoAutomation...
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
echo [4/9] Installing InvektoAgentAI...
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
echo [5/9] Installing InvektoOutbound...
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

REM Knowledge Service
echo [6/9] Installing InvektoKnowledge...
%NSSM% install InvektoKnowledge "E:\Invekto\Knowledge\current\Invekto.Knowledge.exe"
%NSSM% set InvektoKnowledge DisplayName "Invekto Knowledge"
%NSSM% set InvektoKnowledge Description "Invekto Knowledge RAG Service - Port 7104"
%NSSM% set InvektoKnowledge AppDirectory "E:\Invekto\Knowledge\current"
%NSSM% set InvektoKnowledge AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
%NSSM% set InvektoKnowledge AppStdout "E:\Invekto\Knowledge\logs\service-stdout.log"
%NSSM% set InvektoKnowledge AppStderr "E:\Invekto\Knowledge\logs\service-stderr.log"
%NSSM% set InvektoKnowledge AppStdoutCreationDisposition 4
%NSSM% set InvektoKnowledge AppStderrCreationDisposition 4
%NSSM% set InvektoKnowledge AppRotateFiles 1
%NSSM% set InvektoKnowledge AppRotateBytes 10485760
%NSSM% set InvektoKnowledge Start SERVICE_AUTO_START
%NSSM% set InvektoKnowledge AppExit Default Restart
%NSSM% set InvektoKnowledge AppRestartDelay 5000
echo [OK] InvektoKnowledge installed
echo.

REM Appointments Service
echo [7/9] Installing InvektoAppointments...
%NSSM% install InvektoAppointments "E:\Invekto\Appointments\current\Invekto.Appointments.exe"
%NSSM% set InvektoAppointments DisplayName "Invekto Appointments"
%NSSM% set InvektoAppointments Description "Invekto Appointment Scheduling Engine - Port 7102"
%NSSM% set InvektoAppointments AppDirectory "E:\Invekto\Appointments\current"
%NSSM% set InvektoAppointments AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
%NSSM% set InvektoAppointments AppStdout "E:\Invekto\Appointments\logs\service-stdout.log"
%NSSM% set InvektoAppointments AppStderr "E:\Invekto\Appointments\logs\service-stderr.log"
%NSSM% set InvektoAppointments AppStdoutCreationDisposition 4
%NSSM% set InvektoAppointments AppStderrCreationDisposition 4
%NSSM% set InvektoAppointments AppRotateFiles 1
%NSSM% set InvektoAppointments AppRotateBytes 10485760
%NSSM% set InvektoAppointments Start SERVICE_AUTO_START
%NSSM% set InvektoAppointments AppExit Default Restart
%NSSM% set InvektoAppointments AppRestartDelay 5000
echo [OK] InvektoAppointments installed
echo.

REM Integrations Service
echo [8/9] Installing InvektoIntegrations...
%NSSM% install InvektoIntegrations "E:\Invekto\Integrations\current\Invekto.Integrations.exe"
%NSSM% set InvektoIntegrations DisplayName "Invekto Integrations"
%NSSM% set InvektoIntegrations Description "Invekto Marketplace & Cargo Integrations - Port 7106"
%NSSM% set InvektoIntegrations AppDirectory "E:\Invekto\Integrations\current"
%NSSM% set InvektoIntegrations AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
%NSSM% set InvektoIntegrations AppStdout "E:\Invekto\Integrations\logs\service-stdout.log"
%NSSM% set InvektoIntegrations AppStderr "E:\Invekto\Integrations\logs\service-stderr.log"
%NSSM% set InvektoIntegrations AppStdoutCreationDisposition 4
%NSSM% set InvektoIntegrations AppStderrCreationDisposition 4
%NSSM% set InvektoIntegrations AppRotateFiles 1
%NSSM% set InvektoIntegrations AppRotateBytes 10485760
%NSSM% set InvektoIntegrations Start SERVICE_AUTO_START
%NSSM% set InvektoIntegrations AppExit Default Restart
%NSSM% set InvektoIntegrations AppRestartDelay 5000
echo [OK] InvektoIntegrations installed
echo.

REM WhatsAppAnalytics Service
echo [9/11] Installing InvektoWhatsAppAnalytics...
%NSSM% install InvektoWhatsAppAnalytics "E:\Invekto\WhatsAppAnalytics\current\Invekto.WhatsAppAnalytics.exe"
%NSSM% set InvektoWhatsAppAnalytics DisplayName "Invekto WhatsAppAnalytics"
%NSSM% set InvektoWhatsAppAnalytics Description "Invekto WhatsApp Analytics Pipeline - Port 7109"
%NSSM% set InvektoWhatsAppAnalytics AppDirectory "E:\Invekto\WhatsAppAnalytics\current"
%NSSM% set InvektoWhatsAppAnalytics AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
%NSSM% set InvektoWhatsAppAnalytics AppStdout "E:\Invekto\WhatsAppAnalytics\logs\service-stdout.log"
%NSSM% set InvektoWhatsAppAnalytics AppStderr "E:\Invekto\WhatsAppAnalytics\logs\service-stderr.log"
%NSSM% set InvektoWhatsAppAnalytics AppStdoutCreationDisposition 4
%NSSM% set InvektoWhatsAppAnalytics AppStderrCreationDisposition 4
%NSSM% set InvektoWhatsAppAnalytics AppRotateFiles 1
%NSSM% set InvektoWhatsAppAnalytics AppRotateBytes 10485760
%NSSM% set InvektoWhatsAppAnalytics Start SERVICE_AUTO_START
%NSSM% set InvektoWhatsAppAnalytics AppExit Default Restart
%NSSM% set InvektoWhatsAppAnalytics AppRestartDelay 5000
echo [OK] InvektoWhatsAppAnalytics installed
echo.

REM Marketing Service
echo [10/11] Installing InvektoMarketing...
%NSSM% install InvektoMarketing "E:\Invekto\Marketing\current\Invekto.Marketing.exe"
%NSSM% set InvektoMarketing DisplayName "Invekto Marketing"
%NSSM% set InvektoMarketing Description "Invekto Marketing Campaign Engine - Port 7112"
%NSSM% set InvektoMarketing AppDirectory "E:\Invekto\Marketing\current"
%NSSM% set InvektoMarketing AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
%NSSM% set InvektoMarketing AppStdout "E:\Invekto\Marketing\logs\service-stdout.log"
%NSSM% set InvektoMarketing AppStderr "E:\Invekto\Marketing\logs\service-stderr.log"
%NSSM% set InvektoMarketing AppStdoutCreationDisposition 4
%NSSM% set InvektoMarketing AppStderrCreationDisposition 4
%NSSM% set InvektoMarketing AppRotateFiles 1
%NSSM% set InvektoMarketing AppRotateBytes 10485760
%NSSM% set InvektoMarketing Start SERVICE_AUTO_START
%NSSM% set InvektoMarketing AppExit Default Restart
%NSSM% set InvektoMarketing AppRestartDelay 5000
echo [OK] InvektoMarketing installed
echo.

REM WebChat Service
echo [11/11] Installing InvektoWebChat...
%NSSM% install InvektoWebChat "E:\Invekto\WebChat\current\Invekto.WebChat.exe"
%NSSM% set InvektoWebChat DisplayName "Invekto WebChat"
%NSSM% set InvektoWebChat Description "Invekto WebChat Real-time Chat - Port 7113"
%NSSM% set InvektoWebChat AppDirectory "E:\Invekto\WebChat\current"
%NSSM% set InvektoWebChat AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
%NSSM% set InvektoWebChat AppStdout "E:\Invekto\WebChat\logs\service-stdout.log"
%NSSM% set InvektoWebChat AppStderr "E:\Invekto\WebChat\logs\service-stderr.log"
%NSSM% set InvektoWebChat AppStdoutCreationDisposition 4
%NSSM% set InvektoWebChat AppStderrCreationDisposition 4
%NSSM% set InvektoWebChat AppRotateFiles 1
%NSSM% set InvektoWebChat AppRotateBytes 10485760
%NSSM% set InvektoWebChat Start SERVICE_AUTO_START
%NSSM% set InvektoWebChat AppExit Default Restart
%NSSM% set InvektoWebChat AppRestartDelay 5000
echo [OK] InvektoWebChat installed
echo.

REM Create log directories
if not exist "E:\Invekto\Backend\logs" mkdir "E:\Invekto\Backend\logs"
if not exist "E:\Invekto\ChatAnalysis\logs" mkdir "E:\Invekto\ChatAnalysis\logs"
if not exist "E:\Invekto\Automation\logs" mkdir "E:\Invekto\Automation\logs"
if not exist "E:\Invekto\AgentAI\logs" mkdir "E:\Invekto\AgentAI\logs"
if not exist "E:\Invekto\Outbound\logs" mkdir "E:\Invekto\Outbound\logs"
if not exist "E:\Invekto\Knowledge\logs" mkdir "E:\Invekto\Knowledge\logs"
if not exist "E:\Invekto\Appointments\logs" mkdir "E:\Invekto\Appointments\logs"
if not exist "E:\Invekto\Integrations\logs" mkdir "E:\Invekto\Integrations\logs"
if not exist "E:\Invekto\WhatsAppAnalytics\logs" mkdir "E:\Invekto\WhatsAppAnalytics\logs"
if not exist "E:\Invekto\WhatsAppAnalytics\uploads" mkdir "E:\Invekto\WhatsAppAnalytics\uploads"
if not exist "E:\Invekto\Marketing\logs" mkdir "E:\Invekto\Marketing\logs"
if not exist "E:\Invekto\WebChat\logs" mkdir "E:\Invekto\WebChat\logs"

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
%NSSM% start InvektoKnowledge
timeout /t 3 /nobreak >nul
%NSSM% start InvektoAppointments
timeout /t 3 /nobreak >nul
%NSSM% start InvektoIntegrations
timeout /t 3 /nobreak >nul
%NSSM% start InvektoWhatsAppAnalytics
timeout /t 3 /nobreak >nul
%NSSM% start InvektoMarketing
timeout /t 3 /nobreak >nul
%NSSM% start InvektoWebChat
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
%NSSM% status InvektoKnowledge
%NSSM% status InvektoAppointments
%NSSM% status InvektoIntegrations
%NSSM% status InvektoWhatsAppAnalytics
%NSSM% status InvektoMarketing
%NSSM% status InvektoWebChat
echo.
echo Test:
echo   http://localhost:5000/health
echo   http://localhost:7101/health
echo   http://localhost:7102/health
echo   http://localhost:7104/health
echo   http://localhost:7105/health
echo   http://localhost:7106/health
echo   http://localhost:7107/health
echo   http://localhost:7108/health
echo   http://localhost:7109/health
echo   http://localhost:7112/health
echo   http://localhost:7113/health
echo.
echo Manage:
echo   %NSSM% edit InvektoBackend
echo   %NSSM% edit InvektoChatAnalysis
echo   %NSSM% edit InvektoAutomation
echo   %NSSM% edit InvektoAgentAI
echo   %NSSM% edit InvektoOutbound
echo   %NSSM% edit InvektoKnowledge
echo   %NSSM% edit InvektoAppointments
echo   %NSSM% edit InvektoIntegrations
echo   %NSSM% edit InvektoWhatsAppAnalytics
echo   %NSSM% edit InvektoMarketing
echo   %NSSM% edit InvektoWebChat
echo.
pause
