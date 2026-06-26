<#
.SYNOPSIS
  Install POS2SAP as Windows Service (runs API + web UI on one port).

.PARAMETER InstallPath
  Folder containing POS2SAP.API.exe (default: script directory)

.PARAMETER Port
  HTTP port (default: 8080). Overridden if Kestrel:Endpoints is set in appsettings.Production.json

.EXAMPLE
  # Run as Administrator from the release folder:
  .\install.ps1 -Port 8080
#>
param(
    [string]$InstallPath = $PSScriptRoot,
    [int]$Port = 8080
)

$ErrorActionPreference = 'Stop'
$ServiceName = 'POS2SAP'
$DisplayName = 'POS2SAP Interface Service'

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p = New-Object Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    Write-Error "Run PowerShell as Administrator to install the Windows Service."
    exit 1
}

$InstallPath = (Resolve-Path $InstallPath).Path
$ExePath = Join-Path $InstallPath 'POS2SAP.API.exe'
if (-not (Test-Path $ExePath)) {
    Write-Error "POS2SAP.API.exe not found in $InstallPath"
    exit 1
}

$ProdConfig = Join-Path $InstallPath 'appsettings.Production.json'
$ProdExample = Join-Path $InstallPath 'appsettings.Production.example.json'
if (-not (Test-Path $ProdConfig)) {
    if (Test-Path $ProdExample) {
        Copy-Item $ProdExample $ProdConfig
        Write-Host "Created appsettings.Production.json from example — edit DB + JWT before starting."
    } else {
        Write-Warning "appsettings.Production.json not found. Create it before starting the service."
    }
}

New-Item -ItemType Directory -Path (Join-Path $InstallPath 'Logs') -Force | Out-Null

$binPath = "`"$ExePath`" --urls http://0.0.0.0:$Port"

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Stopping existing service $ServiceName..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Creating service $ServiceName -> $binPath"
New-Service -Name $ServiceName `
    -DisplayName $DisplayName `
    -Description 'POS to SAP B1 interface (auto import/send + web monitor)' `
    -BinaryPathName $binPath `
    -StartupType Automatic | Out-Null

$regPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
$envKey  = Join-Path $regPath 'Environment'
New-Item -Path $envKey -Force | Out-Null
Set-ItemProperty -Path $envKey -Name 'ASPNETCORE_ENVIRONMENT' -Value 'Production'

Write-Host "Starting service..."
Start-Service -Name $ServiceName

Write-Host ""
Write-Host "Installed. Open http://localhost:$Port"
Write-Host "Health: http://localhost:$Port/health"
Write-Host "Logs:   $InstallPath\Logs"
