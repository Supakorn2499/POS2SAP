$procs = Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*POS2SAP.API*' }
if ($procs) {
  foreach ($p in $procs) {
    Write-Output "Stopping PID=$($p.Id) Path=$($p.Path)"
    try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } catch {}
  }
} else {
  Write-Output 'No matching dotnet processes found'
}
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Select-Object Id,Path
