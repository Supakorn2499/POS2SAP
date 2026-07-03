<#
.SYNOPSIS
  Build portable release: self-contained .NET (no runtime install) + Start/Stop batch files.

.OUTPUT
  dist/portable/  — copy folder anywhere, double-click "Start POS2SAP.bat"

.EXAMPLE
  .\scripts\deploy\build-portable.ps1
  .\scripts\deploy\build-portable.ps1 -Runtime win-x64
#>
param(
    [ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$RepoRoot     = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$PortableDir  = Join-Path $RepoRoot 'dist\portable'
$ApiDir       = Join-Path $RepoRoot 'backend\POS2SAP.API'
$UiDir        = Join-Path $RepoRoot 'frontend\pos2sap-ui'

Write-Host "==> POS2SAP portable build ($Runtime, self-contained)"
Write-Host "    Output: $PortableDir"

if (Test-Path $PortableDir) {
    Remove-Item $PortableDir -Recurse -Force
}
New-Item -ItemType Directory -Path $PortableDir | Out-Null

Write-Host "==> dotnet publish (Release, self-contained)..."
Push-Location $ApiDir
dotnet publish -c Release -o $PortableDir -r $Runtime --self-contained true
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

$WwwRoot = Join-Path $PortableDir 'wwwroot'
New-Item -ItemType Directory -Path $WwwRoot -Force | Out-Null
Copy-Item -Path (Join-Path $UiDir 'dist\*') -Destination $WwwRoot -Recurse -Force

# Portable launchers + optional service install for later
@(
    'Start POS2SAP.bat',
    'Stop POS2SAP.bat',
    'install.ps1',
    'uninstall.ps1'
) | ForEach-Object {
    Copy-Item (Join-Path $PSScriptRoot $_) $PortableDir
}

Copy-Item (Join-Path $ApiDir 'appsettings.Production.example.json') $PortableDir
New-Item -ItemType Directory -Path (Join-Path $PortableDir 'Logs') -Force | Out-Null

@('appsettings.Development.json', 'package.json', 'package-lock.json') | ForEach-Object {
    $p = Join-Path $PortableDir $_
    if (Test-Path $p) { Remove-Item $p -Force }
}

Write-Host ""
Write-Host "==> Done. Portable folder ready:"
Write-Host "    $PortableDir"
Write-Host ""
Write-Host "On target machine (no .NET install required):"
Write-Host "  1. Copy dist\portable to e.g. C:\POS2SAP"
Write-Host "  2. Copy appsettings.Production.example.json -> appsettings.Production.json and edit"
Write-Host "  3. Double-click 'Start POS2SAP.bat'"
Write-Host "  4. Use 'Stop POS2SAP.bat' to stop (or close the minimized console window)"
