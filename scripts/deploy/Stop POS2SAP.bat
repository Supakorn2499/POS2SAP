@echo off
setlocal EnableExtensions
cd /d "%~dp0"

tasklist /FI "IMAGENAME eq POS2SAP.API.exe" 2>nul | find /I /C "POS2SAP.API.exe" >nul
if not %ERRORLEVEL%==0 (
    echo [POS2SAP] Not running.
    pause
    exit /b 0
)

taskkill /IM POS2SAP.API.exe /F >nul 2>&1
if %ERRORLEVEL%==0 (
    echo [POS2SAP] Stopped.
) else (
    echo [POS2SAP] Could not stop POS2SAP.API.exe
)
pause
