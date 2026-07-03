# POS2SAP — Production Deploy (Windows)

โฟลเดอร์เดียว = API + Web UI + Background scheduler

| โหมด | เหมาะกับ | ต้องติด .NET บนเครื่องปลายทาง |
|------|----------|-------------------------------|
| **Portable** (แนะนำเริ่มต้น) | คลิกเปิดใช้ / ทดสอบ / เครื่องสาขา | ไม่ต้อง (self-contained) |
| **Windows Service** | Server 24/7 production | ต้อง (หรือใช้ portable build แล้ว install service ก็ได้) |

## สิ่งที่ต้องมีบนเครื่องปลายทาง

| รายการ | Portable | Service |
|--------|----------|---------|
| Windows Server 2019+ หรือ Windows 10/11 | ✅ | ✅ |
| .NET 8 Runtime | ไม่ต้อง | ต้อง (ถ้า build แบบ `build-release.ps1`) |
| SQL Server (`HQ_FAMTIME`) | ✅ เครือข่ายถึง DB | ✅ |
| เครือข่ายไป SAP | ✅ | ✅ |

---

## โหมด A — Portable (คลิกเปิดใช้)

### Build บนเครื่อง dev

```powershell
cd D:\SUDEV\Projects\POS2SAP
.\scripts\deploy\build-portable.ps1
```

ได้โฟลเดอร์ `dist\portable\`:

- `POS2SAP.API.exe` + runtime (self-contained, ~100MB+)
- `wwwroot\` — React UI
- `Start POS2SAP.bat` — ดับเบิลคลิกเปิดแอป + เปิดเบราว์เซอร์
- `Stop POS2SAP.bat` — หยุดแอป
- `install.ps1` / `uninstall.ps1` — อัปเกรดเป็น Windows Service ภายหลังได้
- `appsettings.Production.example.json`

### ใช้งานบนเครื่องปลายทาง

1. Copy ทั้งโฟลเดอร์ `dist\portable` ไป เช่น `C:\POS2SAP`
2. Copy `appsettings.Production.example.json` → `appsettings.Production.json` แล้วแก้ DB + JWT
3. รัน `sql/init.sql` บน SQL Server (ครั้งแรก)
4. **ดับเบิลคลิก `Start POS2SAP.bat`**
5. เปิด UI ที่ `http://localhost:8080` (เบราว์เซอร์เปิดให้อัตโนมัติ)
6. หยุดด้วย `Stop POS2SAP.bat`

Scheduler ทำงานในพื้นหลังขณะ `POS2SAP.API.exe` รัน — ไม่ต้องเปิดเบราว์เซอร์ค้างไว้

> พอร์ต default `8080` ตั้งใน `appsettings.Production.json` → `Kestrel:Endpoints`. ถ้าเปลี่ยนพอร์ต แก้ `PORT=8080` ใน `Start POS2SAP.bat` ให้ตรงกัน

---

## โหมด B — Windows Service (server 24/7)

### Build บนเครื่อง dev

```powershell
cd D:\SUDEV\Projects\POS2SAP
.\scripts\deploy\build-release.ps1
```

ได้โฟลเดอร์ `dist\release\` ประกอบด้วย:

- `POS2SAP.API.exe` + DLL (ต้องติด .NET 8 Runtime บน server)
- `wwwroot\` — React UI (เรียก API ที่ `/api` same-origin)
- `install.ps1` / `uninstall.ps1`
- `appsettings.Production.example.json`

## Copy ไปเซิร์ฟเวอร์ (โหมด Service)

คัดลอกทั้งโฟลเดอร์ `dist\release` ไป เช่น `C:\POS2SAP`

## ตั้งค่า (ทั้ง Portable และ Service)

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

## ติดตั้ง Windows Service (โหมด B เท่านั้น)

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
