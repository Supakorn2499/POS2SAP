<#
Runs the ensure_auth_schema.sql against the database specified in appsettings.json
Requires: `sqlcmd` in PATH
Usage: .\run_db_setup.ps1
#>

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$appsettings = Join-Path $root '..\appsettings.json' | Resolve-Path
if (-not (Test-Path $appsettings)) {
    Write-Error "appsettings.json not found at $appsettings"
    exit 1
}

$json = Get-Content $appsettings -Raw | ConvertFrom-Json
$conn = $json.ConnectionStrings.DefaultConnection

# Parse connection string like: Server=host,port;Database=DB;User Id=user;Password=pass;...
$srvMatch = [regex]::Match($conn, 'Server=([^;]+)')
$dbMatch  = [regex]::Match($conn, 'Database=([^;]+)')
$userMatch = [regex]::Match($conn, 'User Id=([^;]+)')
$passMatch = [regex]::Match($conn, 'Password=([^;]+)')

if (-not $srvMatch.Success -or -not $dbMatch.Success -or -not $userMatch.Success -or -not $passMatch.Success) {
    Write-Error "Failed to parse connection string from appsettings.json"
    exit 1
}

$server = $srvMatch.Groups[1].Value
$database = $dbMatch.Groups[1].Value
$user = $userMatch.Groups[1].Value
$pass = $passMatch.Groups[1].Value

$sqlFile = Join-Path $root '..\sql\ensure_auth_schema.sql' | Resolve-Path

Write-Host "Running $sqlFile against $server/$database"

$cmd = "sqlcmd -S `"$server`" -d `"$database`" -U `"$user`" -P `"$pass`" -i `"$sqlFile`""

Write-Host $cmd
Invoke-Expression $cmd
