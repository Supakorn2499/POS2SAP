$login = Invoke-RestMethod -Uri 'http://localhost:5163/api/auth/login' -Method Post -ContentType 'application/json' -Body (ConvertTo-Json @{ StaffLogin = 'ciuser'; StaffPassword = 'Test@1234' })
$token = $login.data.accessToken
$resp = Invoke-RestMethod -Uri "http://localhost:5163/api/monitor/logs?interfaceType=ARInvoice&status=FAILED&page=1&pageSize=10" -Headers @{ Authorization = "Bearer $token" } -Method Get
$resp | ConvertTo-Json -Depth 6
