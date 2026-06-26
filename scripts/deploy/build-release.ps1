<#
.SYNOPSIS
  Build production release: API + React UI in one folder (wwwroot).

.OUTPUT
  dist/release/  — copy this folder to the server, then run install.ps1

.EXAMPLE
  .\scripts\deploy\build-release.ps1
#>
$ErrorActionPreference = 'Stop'

$RepoRoot   = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$ReleaseDir = Join-Path $RepoRoot 'dist\release'
$ApiDir     = Join-Path $RepoRoot 'backend\POS2SAP.API'
$UiDir      = Join-Path $RepoRoot 'frontend\pos2sap-ui'

Write-Host "==> POS2SAP release build"
Write-Host "    Output: $ReleaseDir"

if (Test-Path $ReleaseDir) {
    Remove-Item $ReleaseDir -Recurse -Force
}
New-Item -ItemType Directory -Path $ReleaseDir | Out-Null

Write-Host "==> dotnet publish (Release)..."
Push-Location $ApiDir
dotnet publish -c Release -o $ReleaseDir --no-self-contained
if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }
Pop-Location

Write-Host "==> npm run build (frontend)..."
Push-Location $UiDir
if (-not (Test-Path 'node_modules')) {
    npm ci
    if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }
}
npm run build
if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }
Pop-Location

$WwwRoot = Join-Path $ReleaseDir 'wwwroot'
New-Item -ItemType Directory -Path $WwwRoot -Force | Out-Null
Copy-Item -Path (Join-Path $UiDir 'dist\*') -Destination $WwwRoot -Recurse -Force

# Deploy helpers + config template
Copy-Item (Join-Path $PSScriptRoot 'install.ps1') $ReleaseDir
Copy-Item (Join-Path $PSScriptRoot 'uninstall.ps1') $ReleaseDir
Copy-Item (Join-Path $ApiDir 'appsettings.Production.example.json') $ReleaseDir

New-Item -ItemType Directory -Path (Join-Path $ReleaseDir 'Logs') -Force | Out-Null

# Do not ship dev secrets or Node artifacts
@('appsettings.Development.json', 'package.json', 'package-lock.json') | ForEach-Object {
    $p = Join-Path $ReleaseDir $_
    if (Test-Path $p) { Remove-Item $p -Force }
}

Write-Host ""
Write-Host "==> Done. Release folder ready:"
Write-Host "    $ReleaseDir"
Write-Host ""
Write-Host "Next on server:"
Write-Host "  1. Copy dist\release to e.g. C:\POS2SAP"
Write-Host "  2. Copy appsettings.Production.example.json -> appsettings.Production.json and edit"
Write-Host "  3. Run init.sql on SQL Server if not done yet"
Write-Host "  4. Run install.ps1 as Administrator"
