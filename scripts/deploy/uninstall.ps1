<#
.SYNOPSIS
  Stop and remove POS2SAP Windows Service.
#>
$ErrorActionPreference = 'Stop'
$ServiceName = 'POS2SAP'

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $svc) {
    Write-Host "Service $ServiceName is not installed."
    exit 0
}

Write-Host "Stopping $ServiceName..."
Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
sc.exe delete $ServiceName | Out-Null
Write-Host "Service removed."
