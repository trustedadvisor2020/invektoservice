@echo off
REM ============================================
REM  INVEKTO - Quick Service Restart
REM  Run as Administrator on server
REM ============================================

set "NSSM=E:\nssm.exe"

echo.
echo Stopping services...
%NSSM% stop InvektoBackend
%NSSM% stop InvektoChatAnalysis
%NSSM% stop InvektoAutomation
timeout /t 3 /nobreak >nul

echo.
echo Starting services...
%NSSM% start InvektoBackend
timeout /t 2 /nobreak >nul
%NSSM% start InvektoChatAnalysis
timeout /t 2 /nobreak >nul
%NSSM% start InvektoAutomation
timeout /t 2 /nobreak >nul

echo.
echo Status:
%NSSM% status InvektoBackend
%NSSM% status InvektoChatAnalysis
%NSSM% status InvektoAutomation
echo.
echo Test: http://localhost:5000/health
echo Test: http://localhost:7101/health
echo Test: http://localhost:7108/health
echo.
pause
