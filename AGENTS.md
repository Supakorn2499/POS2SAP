# POS2SAP — Agent Guide

Background-job integration that reads POS transactions from `HQ_FAMTIME` (SQL Server), transforms them to SAP AR invoices, and posts to SAP via HTTP. Ships with a React monitoring/admin UI.

Related docs:
- [SECURITY_PHASE1_SETUP.md](SECURITY_PHASE1_SETUP.md) — JWT/BCrypt/CORS/rate-limit setup details
- [backend/POS2SAP.API/sql/SETUP_GUIDE.md](backend/POS2SAP.API/sql/SETUP_GUIDE.md) — DB init steps
- [backend/POS2SAP.API/sql/init.sql](backend/POS2SAP.API/sql/init.sql) — schema source of truth
- `README.md` is UTF-16 encoded — read as binary or convert before parsing

## Stack

- **Backend**: .NET 8 ASP.NET Core, **Dapper** (not EF Core), Serilog, JWT, BCrypt, ULID, Swashbuckle. Entry: [backend/POS2SAP.API/Program.cs](backend/POS2SAP.API/Program.cs).
- **Frontend**: React 19 + TypeScript (strict) + Vite, React Router v7, TanStack Query, Axios, Tailwind, Recharts, Sonner. Entry: [frontend/pos2sap-ui/src/main.tsx](frontend/pos2sap-ui/src/main.tsx).
- **DB**: SQL Server, database `HQ_FAMTIME` (shared with POS). Connection string in [backend/POS2SAP.API/appsettings.json](backend/POS2SAP.API/appsettings.json).

## Build & Run

Backend (http://localhost:5163, Swagger at `/swagger`):
```
cd backend/POS2SAP.API
dotnet restore
dotnet run
```

Frontend (http://localhost:5173, proxies `/api` → backend per [frontend/pos2sap-ui/vite.config.ts](frontend/pos2sap-ui/vite.config.ts)):
```
cd frontend/pos2sap-ui
npm install
npm run dev         # dev
npm run build       # tsc + vite build
npm run lint
```

No test projects exist yet — do not invent `dotnet test` / `npm test` commands.

## Architecture

```
Controllers  →  Services (Interfaces/Implementations)  →  Dapper + SQL
                    │
                    ├─ IPosDataService       (read POS ordertransaction/orderdetail)
                    ├─ ISapArInvoiceService  (HTTP POST to SAP, retry 2s/4s/8s)
                    ├─ IInterfaceJobService  (BackgroundService — polls every N min)
                    └─ IInterfaceMonitorService (dashboard, logs, config CRUD)
```

Job flow: scheduler → fetch pending POS docs → map to `SapArInvoiceRequestDto` → POST SAP → write full audit to `interface_logs` (status `PENDING`/`PROCESSING`/`SUCCESS`/`FAILED`/`RETRY`).

Auth: JWT bearer. Public routes: `/api/auth/login`, `/api/auth/refresh`, `/swagger/*`, `/health`. All other controllers use `[Authorize]` ([backend/POS2SAP.API/Attributes/AuthorizeAttribute.cs](backend/POS2SAP.API/Attributes/AuthorizeAttribute.cs)) checked by [backend/POS2SAP.API/Middleware/AuthenticationMiddleware.cs](backend/POS2SAP.API/Middleware/AuthenticationMiddleware.cs).

## Conventions

**Backend (C#)**
- **Every controller action returns `ApiResponse<T>`** — see [backend/POS2SAP.API/Common/ApiResponse.cs](backend/POS2SAP.API/Common/ApiResponse.cs). Use `ApiResponse<T>.Ok(...)` / `.Fail(...)`. Never return raw DTOs.
- **Constants live in [backend/POS2SAP.API/Common/gbVar.cs](backend/POS2SAP.API/Common/gbVar.cs)** — status strings (`StatusPending` etc.), SAP fixed values (`SapDocCur="THB"`, `SapVatPrcnt=7m`), and `interface_configs` keys (`CfgSapUrlTest`, `CfgSapApiKey`, `CfgScheduleIntervalMinutes`, …). Reuse these rather than hardcoding strings.
- **Data access is Dapper over `SqlConnection`** — do not introduce EF Core.
- **Primary keys are ULID** (`NUlid` package) for log rows.
- Logging via Serilog; files written to `backend/POS2SAP.API/Logs/pos2sap-.log` (daily rolling).

**Frontend (TS)**
- API calls go through [frontend/pos2sap-ui/src/services/apiClient.ts](frontend/pos2sap-ui/src/services/apiClient.ts) (baseURL `/api`, JWT from `localStorage['pos2sapToken']`). Don't instantiate raw `axios` elsewhere.
- Server state uses TanStack Query; user feedback via `sonner` toasts.
- DTO TS types in `src/types/` mirror backend DTO names (e.g. `LoginResultDto`).
- UI strings are Thai; keep code identifiers and comments in English.
- Strict TS — keep it compiling with `npm run build`.

## Gotchas

- **JWT secret**: `appsettings.json` ships with a placeholder `Jwt.Secret`. Rotate before any shared deployment. Generator snippet in [SECURITY_PHASE1_SETUP.md](SECURITY_PHASE1_SETUP.md).
- **DB is not auto-migrated**: run [backend/POS2SAP.API/sql/init.sql](backend/POS2SAP.API/sql/init.sql) on a fresh DB or `interface_logs` / `interface_configs` / `refresh_tokens` will be missing.
- **SAP credentials are config rows, not appsettings**: `sap_url_test`, `sap_api_key`, `sap_auth_type`, etc. must exist in `interface_configs` or SAP posts fail.
- **CORS is an explicit whitelist** in `appsettings.json → AllowedOrigins`. Add new frontend origins there.
- **Background job toggle**: config row `schedule_enabled` — if `"false"`, only manual `/api/interface/trigger` works.
- **Legacy SHA1 password path** still exists in [backend/POS2SAP.API/Controllers/AuthController.cs](backend/POS2SAP.API/Controllers/AuthController.cs) for transition; new passwords must be BCrypt (workFactor=11).
- **Vite dev proxy** means calling `/api/...` works in dev without CORS; production build must be served same-origin or `AllowedOrigins` updated.
- Logs folder must be writable; missing `Logs/` directory causes Serilog startup errors on some hosts.

## Pattern-exemplar files

Before adding similar features, skim:
- New protected endpoint → [backend/POS2SAP.API/Controllers/MonitorController.cs](backend/POS2SAP.API/Controllers/MonitorController.cs) + [backend/POS2SAP.API/Services/Implementations/InterfaceMonitorService.cs](backend/POS2SAP.API/Services/Implementations/InterfaceMonitorService.cs)
- SAP HTTP + retry → [backend/POS2SAP.API/Services/Implementations/SapArInvoiceService.cs](backend/POS2SAP.API/Services/Implementations/SapArInvoiceService.cs)
- Background scheduler → [backend/POS2SAP.API/Services/Implementations/InterfaceJobService.cs](backend/POS2SAP.API/Services/Implementations/InterfaceJobService.cs)
- Dapper query patterns → [backend/POS2SAP.API/Services/Implementations/PosDataService.cs](backend/POS2SAP.API/Services/Implementations/PosDataService.cs)
- New UI page with Query + table → [frontend/pos2sap-ui/src/pages/MonitorPage.tsx](frontend/pos2sap-ui/src/pages/MonitorPage.tsx)
- JSON detail layout → [frontend/pos2sap-ui/src/pages/MonitorDetailPage.tsx](frontend/pos2sap-ui/src/pages/MonitorDetailPage.tsx)
