@echo off
REM ============================================================
REM  Knowledge Service Test Suite - Wrapper
REM  Calistirma: test-knowledge.bat
REM  Parametreler: test-knowledge.bat [TenantId] [ConfigPath]
REM ============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0test-knowledge.ps1" %*
