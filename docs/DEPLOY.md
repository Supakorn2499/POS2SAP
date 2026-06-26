# POS2SAP — Production Deploy (Windows)

ติดตั้งแบบ **ง่าย**: โฟลเดอร์เดียว = API + Web UI + Background scheduler (Windows Service)

## สิ่งที่ต้องมีบนเซิร์ฟเวอร์

| รายการ | หมายเหตุ |
|--------|----------|
| Windows Server 2019+ หรือ Windows 10/11 | |
| [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) | Hosting Bundle หรือ ASP.NET Core Runtime |
| SQL Server | เชื่อม `HQ_FAMTIME` (รัน `sql/init.sql` ครั้งแรก) |
| เครือข่ายไป SAP UAT/PRD | ตั้งค่าใน `interface_configs` |

## ขั้นตอน 1 — Build บนเครื่อง dev

```powershell
cd D:\SUDEV\Projects\POS2SAP
.\scripts\deploy\build-release.ps1
```

ได้โฟลเดอร์ `dist\release\` ประกอบด้วย:

- `POS2SAP.API.exe` + DLL
- `wwwroot\` — React UI (เรียก API ที่ `/api` same-origin)
- `install.ps1` / `uninstall.ps1`
- `appsettings.Production.example.json`

## ขั้นตอน 2 — Copy ไปเซิร์ฟเวอร์

คัดลอกทั้งโฟลเดอร์ `dist\release` ไป เช่น `C:\POS2SAP`

## ขั้นตอน 3 — ตั้งค่า

```powershell
cd C:\POS2SAP
copy appsettings.Production.example.json appsettings.Production.json
notepad appsettings.Production.json
```

แก้ให้ครบ:

1. **ConnectionStrings.DefaultConnection** — SQL Server จริง
2. **Jwt.Secret** — สุ่มอย่างน้อย 32 ตัวอักษร (ห้ามใช้ค่า dev)
3. **Kestrel:Endpoints** — พอร์ตที่ต้องการ (default `8080`)
4. **AllowedOrigins** — URL ที่เข้า UI (ถ้า same-origin ใช้ `http://server:8080`)

ตั้ง environment (แนะนำ):

```powershell
[System.Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Production', 'Machine')
```

หรือสร้างไฟล์ `C:\POS2SAP\.env` ไม่ได้ใช้ — ใช้ `appsettings.Production.json` เท่านั้น

## ขั้นตอน 4 — ติดตั้ง Windows Service

เปิด **PowerShell as Administrator**:

```powershell
cd C:\POS2SAP
.\install.ps1 -Port 8080
```

- Service name: `POS2SAP`
- เปิดเบราว์เซอร์: `http://localhost:8080`
- Health: `http://localhost:8080/health`
- Log: `C:\POS2SAP\Logs\pos2sap-*.log`

## ถอนการติดตั้ง

```powershell
.\uninstall.ps1
```

## Checklist ก่อน Go-live

- [ ] รัน `backend/POS2SAP.API/sql/init.sql` บน DB
- [ ] รัน `sql/ensure_auth_schema.sql` (refresh_tokens)
- [ ] ตั้ง SAP URL / API key ใน Config หรือ `interface_configs`
- [ ] ตั้ง `interface_cutover_date`, `schedule_enabled`, ช่วงเวลา auto
- [ ] Mapping: GL, Product Group, Delivery Doc Types
- [ ] ลบแถวทดสอบ `staffs` (StaffID=0) ถ้ามี
- [ ] Login ด้วยบัญชี POS จริง (เช่น `vtec`)
- [ ] ทดสอบ manual trigger ก่อนเปิด scheduler
- [ ] (Optional) รัน `sql/perf_indexes.sql` หลัง DBA review

## โหมด dev vs production

| | Dev | Production |
|---|-----|------------|
| Frontend | Vite `:5173` | รวมใน `wwwroot` |
| Backend | `dotnet run :5163` | Windows Service `:8080` |
| Scheduler | `InterfaceJobService` ใน process เดียวกัน | เหมือนกัน — **ไม่ต้องเปิดเบราว์เซอร์** |
| Swagger | เปิดใน Development | ปิด |

## Firewall

เปิด inbound TCP พอร์ตที่ใช้ (เช่น 8080) สำหรับผู้ใช้ในเครือข่าย
