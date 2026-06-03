$root = Split-Path -Parent $MyInvocation.MyCommand.Path
# appsettings.json lives in the POS2SAP.API folder
# appsettings.json lives in the POS2SAP.API folder which is sibling to this scripts folder
$appsettings = Join-Path $root '..\POS2SAP.API\appsettings.json' | Resolve-Path
if (-not (Test-Path $appsettings)) { Write-Error "appsettings.json not found"; exit 1 }

$json = Get-Content $appsettings -Raw | ConvertFrom-Json
$conn = $json.ConnectionStrings.DefaultConnection

$srvMatch = [regex]::Match($conn, 'Server=([^;]+)')
$dbMatch  = [regex]::Match($conn, 'Database=([^;]+)')
$userMatch = [regex]::Match($conn, 'User Id=([^;]+)')
$passMatch = [regex]::Match($conn, 'Password=([^;]+)')

if (-not $srvMatch.Success -or -not $dbMatch.Success -or -not $userMatch.Success -or -not $passMatch.Success) { Write-Error "Failed to parse connection string"; exit 1 }

$server = $srvMatch.Groups[1].Value
$database = $dbMatch.Groups[1].Value
$user = $userMatch.Groups[1].Value
$pass = $passMatch.Groups[1].Value

$sqlFile = Join-Path $root 'deactivate_global_sap_urls.sql' | Resolve-Path

Write-Host "Running $sqlFile against $server/$database"
$cmd = "sqlcmd -S `"$server`" -d `"$database`" -U `"$user`" -P `"$pass`" -i `"$sqlFile`""
Write-Host $cmd
Invoke-Expression $cmd
