$processes = Get-Process -Name dotnet -ErrorAction SilentlyContinue
foreach ($p in $processes) {
  try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; Write-Host "Stopped dotnet PID=$($p.Id)" } catch { }
}
Start-Sleep -Seconds 1
Set-Location 'D:\SUDEV\Projects\POS2SAP\backend\POS2SAP.API'
dotnet build POS2SAP.API.csproj -v minimal
