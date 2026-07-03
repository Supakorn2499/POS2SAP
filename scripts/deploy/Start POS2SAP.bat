@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "PORT=8080"
set "ASPNETCORE_ENVIRONMENT=Production"

if not exist "appsettings.Production.json" (
    if exist "appsettings.Production.example.json" (
        copy /Y "appsettings.Production.example.json" "appsettings.Production.json" >nul
        echo [POS2SAP] Created appsettings.Production.json from example.
        echo [POS2SAP] Edit DB connection + Jwt.Secret, then run this file again.
        pause
        exit /b 1
    )
    echo [POS2SAP] Missing appsettings.Production.json
    pause
    exit /b 1
)

if not exist "POS2SAP.API.exe" (
    echo [POS2SAP] POS2SAP.API.exe not found in %~dp0
    pause
    exit /b 1
)

if not exist "Logs" mkdir "Logs"

tasklist /FI "IMAGENAME eq POS2SAP.API.exe" 2>nul | find /I /C "POS2SAP.API.exe" >nul
if %ERRORLEVEL%==0 (
    echo [POS2SAP] Already running. Opening browser...
    start "" "http://localhost:%PORT%"
    exit /b 0
)

echo [POS2SAP] Starting (minimized window)...
start "POS2SAP" /MIN "%~dp0POS2SAP.API.exe"

set "TRIES=0"
:wait_health
timeout /t 2 /nobreak >nul
set /a TRIES+=1
powershell -NoProfile -Command "try { (Invoke-WebRequest -UseBasicParsing -TimeoutSec 2 'http://localhost:%PORT%/health').StatusCode -eq 200 | Out-Null; exit 0 } catch { exit 1 }" >nul 2>&1
if %ERRORLEVEL%==0 goto open_browser
if %TRIES% LSS 15 goto wait_health

echo [POS2SAP] Started but health check timed out. Try http://localhost:%PORT% manually.
pause
exit /b 0

:open_browser
start "" "http://localhost:%PORT%"
echo [POS2SAP] Running at http://localhost:%PORT%
echo [POS2SAP] Logs: %~dp0Logs
echo [POS2SAP] Stop with "Stop POS2SAP.bat"
exit /b 0
