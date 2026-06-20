# POS2SAP — Agent Guide

Background-job integration that reads POS transactions from `HQ_FAMTIME` (SQL Server), transforms them to SAP AR invoices, and posts to SAP via HTTP. Ships with a React monitoring/admin UI.

Related docs:
- [SECURITY_PHASE1_SETUP.md](SECURITY_PHASE1_SETUP.md) — JWT/BCrypt/CORS/rate-limit setup details
- [backend/POS2SAP.API/sql/SETUP_GUIDE.md](backend/POS2SAP.API/sql/SETUP_GUIDE.md) — DB init steps
- [backend/POS2SAP.API/sql/init.sql](backend/POS2SAP.API/sql/init.sql) — schema source of truth
- `README.md` is UTF-16 encoded — read as binary or convert before parsing

## Stack

- **Backend**: .NET 8 ASP.NET Core, **Dapper** (not EF Core), Serilog, JWT, BCrypt, ULID, Swashbuckle. Entry: [backend/POS2SAP.API/Program.cs](backend/POS2SAP.API/Program.cs).
- **Frontend**: React 19 + TypeScript + Vite, React Router v7, TanStack Query, Axios, Tailwind, Recharts, Sonner. Entry: [frontend/pos2sap-ui/src/main.tsx](frontend/pos2sap-ui/src/main.tsx).
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
                    ├─ IInterfaceJobService  (BackgroundService — polls every N min; also manual trigger/retry/import)
                    ⚠ Registered twice: `AddScoped<IInterfaceJobService>` (DI for controllers) + `AddHostedService<InterfaceJobService>` (scheduler). Two separate instances — do not expect shared in-memory state.
                    └─ IInterfaceMonitorService (dashboard, logs, config CRUD)
```

Controllers: `AuthController` (public), `ConfigController`, `DebugController`, `InterfaceController`, `MonitorController`.

Frontend pages: `LoginPage`, `DashboardPage`, `MonitorPage`, `MonitorDetailPage`, `ConfigPage`, `ImportPage`.  
Frontend services: `apiClient.ts`, `loginService.ts`, `dashboardService.ts`, `monitorService.ts`, `interfaceService.ts`, `configService.ts`.  
Frontend components: `ConfirmDialog` (modal), `JsonViewer` (pretty-print JSON), `StatusBadge` (color-coded status), `StatCard` (dashboard tile), `layout/AppLayout` (nav/header wrapper).

Job flow: scheduler → fetch pending POS docs → map to `SapArInvoiceRequestDto` → POST SAP → write full audit to `interface_logs` (status `PENDING`/`PROCESSING`/`SUCCESS`/`FAILED`/`RETRY`).

**Middleware order** (Program.cs): SerilogRequest → Swagger (dev) → CORS → `JwtAuthMiddleware` → `AuthorizationMiddleware` → `UseAuthentication` → `UseAuthorization` → MapControllers.

**Frontend routing** (all but `/login` are `RequireAuth`-wrapped): `/login`, `/` → `/dashboard`, `/dashboard`, `/monitor`, `/monitor/:id`, `/config`, `/import`, `*` → `/login`.

Auth: JWT bearer. Public routes: `/api/auth/login`, `/api/auth/refresh`, `/swagger/*`, `/health`. All other controllers use `[Authorize]` ([backend/POS2SAP.API/Attributes/AuthorizeAttribute.cs](backend/POS2SAP.API/Attributes/AuthorizeAttribute.cs)) checked by [backend/POS2SAP.API/Middleware/AuthenticationMiddleware.cs](backend/POS2SAP.API/Middleware/AuthenticationMiddleware.cs).

## Conventions

**Backend (C#)**
- **Every controller action returns `ApiResponse<T>`** — see [backend/POS2SAP.API/Common/ApiResponse.cs](backend/POS2SAP.API/Common/ApiResponse.cs). Use `ApiResponse<T>.Ok(...)` / `.Fail(...)`. Never return raw DTOs.
- **Constants live in [backend/POS2SAP.API/Common/gbVar.cs](backend/POS2SAP.API/Common/gbVar.cs)** — status strings (`StatusPending` etc.), SAP fixed values (`SapDocCur="THB"`, `SapVatPrcnt=7m`), and `interface_configs` keys (`CfgSapUrlTest`, `CfgSapApiKey`, `CfgScheduleIntervalMinutes`, …). Reuse these rather than hardcoding strings.
- **Data access is Dapper over `SqlConnection`** — do not introduce EF Core.
- **Primary keys are ULID** (`NUlid` package) for log rows.
- Logging via Serilog; files written to `backend/POS2SAP.API/Logs/pos2sap-.log` (daily rolling).

**Frontend (TS)**
- API calls go through [frontend/pos2sap-ui/src/services/apiClient.ts](frontend/pos2sap-ui/src/services/apiClient.ts) (baseURL `VITE_API_URL` env or `/api`, JWT from `localStorage['pos2sapToken']`). Don't instantiate raw `axios` elsewhere.
- Server state uses TanStack Query; user feedback via `sonner` toasts.
- DTO TS types in `src/types/` mirror backend DTO names (e.g. `LoginResultDto`). Files: `auth.ts`, `dashboard.ts`, `monitor.ts` (`InterfaceStatus` union, `PagedResult<T>`), `config.ts`, `import.ts`.
- UI strings: `const { t } = useLanguage()` from [LanguageContext.tsx](frontend/pos2sap-ui/src/contexts/LanguageContext.tsx), then `t('key')`. Add new keys (both `en` and `th`) to [src/lib/i18n.ts](frontend/pos2sap-ui/src/lib/i18n.ts). Keep code identifiers and comments in English.
- Import alias `@/` maps to `src/` (e.g., `@/components/StatusBadge`, `@/services/apiClient`); configured in [tsconfig.app.json](frontend/pos2sap-ui/tsconfig.app.json). Use it consistently.
- TS uses some relaxed checks (`noUnusedLocals=false`, `noUnusedParameters=false`) plus `ignoreDeprecations: "6.0"`; keep it compiling with `npm run build`.

## Gotchas

- **JWT secret**: `appsettings.json` ships with a placeholder `Jwt.Secret`. Rotate before any shared deployment. Generator snippet in [SECURITY_PHASE1_SETUP.md](SECURITY_PHASE1_SETUP.md).
- **DB is not auto-migrated**: run [backend/POS2SAP.API/sql/init.sql](backend/POS2SAP.API/sql/init.sql) on a fresh DB or `interface_logs` / `interface_configs` / `refresh_tokens` will be missing.
- **SAP credentials are config rows, not appsettings**: `sap_url_test`, `sap_api_key`, `sap_auth_type`, etc. must exist in `interface_configs` or SAP posts fail.
- **CORS is an explicit whitelist** in `appsettings.json → AllowedOrigins`. Add new frontend origins there.
- **Background job toggle**: config row `schedule_enabled` — if `"false"`, only manual `/api/interface/trigger` works.
- **Legacy SHA1 password path** still exists in [backend/POS2SAP.API/Controllers/AuthController.cs](backend/POS2SAP.API/Controllers/AuthController.cs) for transition; new passwords must be BCrypt (workFactor=11).
- **Vite dev proxy** means calling `/api/...` works in dev without CORS; production build must be served same-origin or `AllowedOrigins` updated.
- **Frontend auth storage keys** are `pos2sapAuth`, `pos2sapToken`, `pos2sapUser` in [frontend/pos2sap-ui/src/contexts/AuthContext.tsx](frontend/pos2sap-ui/src/contexts/AuthContext.tsx); keep key names consistent or auth state breaks.
- **TypeScript config is intentionally mixed-strictness** in [frontend/pos2sap-ui/tsconfig.app.json](frontend/pos2sap-ui/tsconfig.app.json) (includes `ignoreDeprecations: "6.0"` and relaxed unused checks); do not assume full strict-mode diagnostics.
- Logs folder must be writable; missing `Logs/` directory causes Serilog startup errors on some hosts.
- **PosDataService SQL timeout**: Dapper queries against `HQ_FAMTIME` (shared POS DB) can exceed 30s on large datasets. Always use `commandTimeout: 120` in `CommandDefinition` for `PosDataService` queries. The default 30s timeout will cause intermittent failures during bulk send operations.
- **`/api/interface/upload` is `[AllowAnonymous]`** (dev test endpoint in `InterfaceController`) — do not expose in production.
- **`POST /api/monitor/simulate-statuses`** randomizes log statuses in DB — dev only, protected by vtec-user check. Never call in production.
- **Frontend HTTP timeout is 30s** (`apiClient.ts`). Bulk trigger/import operations hit the 120s backend SQL timeout first, so the frontend may show a network error while the backend is still running. Increase `timeout` per-call or add polling for new bulk endpoints.
- **Rate limiting is disabled**: `UseIpRateLimiting()` and all `AspNetCoreRateLimit` service registrations are commented out in `Program.cs`. Uncomment all related lines together to re-enable.
- **`appsettings.Development.json` contains real DB credentials** (live SQL Server IP + credentials). Do not commit mutations to this file or leak it.

## API Endpoints Reference

**InterfaceController** (`/api/interface`):

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| POST | `/trigger` | [Authorize] | Run pending/retry or specific docNos; returns `TriggerResultDto` (Sent, Failed, Total) |
| POST | `/retry/{id}` | [Authorize] | Retry single FAILED log by ID |
| POST | `/preview` | [Authorize] | Fetch POS bills by dateFrom/dateTo/branch/type → return `ImportPreviewItemDto[]` (status NEW/DUP) without writing |
| POST | `/import` | [Authorize] | Pull from POS → insert as PENDING (no SAP send); returns `ImportResultDto` |
| POST | `/resend` | [Authorize] | Batch resend `SapArInvoiceHeadDto[]` from request body |
| POST | `/upload` | AllowAnonymous | Dev test: accept raw JSON, create PENDING log — **do not expose in production** |

**MonitorController** (`/api/monitor`):

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| GET | `/logs` | [Authorize] | Paginated log list; query: page, pageSize(1-100), sortBy, sortDirection, search, status, branch, dateFrom, dateTo |
| GET | `/logs/{id}` | [Authorize] | Full detail including `posData`, `sapRequest`, `sapResponse` JSON |
| GET | `/branches` | [Authorize] | Branch dropdown options (`BranchOptionDto[]`) |
| GET | `/dashboard` | [Authorize] | Summary: status counts, daily trend, top branches, recent logs |
| POST | `/simulate-statuses` | [Authorize] | **DEV ONLY** — randomize log statuses (vtec user check) |

**Auth/Config**:
- `POST /api/auth/login`, `POST /api/auth/refresh` — public
- `GET/PUT /api/config` — full config CRUD
- `PUT /api/debug/config/{key}` — upsert config without auth (dev only)

## Development & Testing

**Scripts** (bash, not part of formal test suite):
- [backend/scripts/test_interface_connections.js](backend/scripts/test_interface_connections.js) — test SAP connectivity for each interface (ARInvoice, IncomingPayment, Delivery)
- [backend/scripts/seed_default_configs.js](backend/scripts/seed_default_configs.js) — seed interface-specific SAP URLs and API keys
- [backend/scripts/test_upload.js](backend/scripts/test_upload.js) — manual POST to `/api/interface/create` for testing
- [backend/scripts/get_configs.js](backend/scripts/get_configs.js) — fetch current config dict

**Debug endpoints** (public, dev-only):
- `PUT /api/debug/config/{key}` — upsert a config without auth (see [backend/POS2SAP.API/Controllers/DebugController.cs](backend/POS2SAP.API/Controllers/DebugController.cs))

**Swagger UI**:
- Visit `http://localhost:5163/swagger` after running backend; all endpoints documented with auth headers shown.

## Pattern-exemplar files

Before adding similar features, skim:
- New protected endpoint → [backend/POS2SAP.API/Controllers/MonitorController.cs](backend/POS2SAP.API/Controllers/MonitorController.cs) + [backend/POS2SAP.API/Services/Implementations/InterfaceMonitorService.cs](backend/POS2SAP.API/Services/Implementations/InterfaceMonitorService.cs)
- SAP HTTP + retry → [backend/POS2SAP.API/Services/Implementations/SapArInvoiceService.cs](backend/POS2SAP.API/Services/Implementations/SapArInvoiceService.cs)
- Background scheduler → [backend/POS2SAP.API/Services/Implementations/InterfaceJobService.cs](backend/POS2SAP.API/Services/Implementations/InterfaceJobService.cs)
- Dapper query patterns → [backend/POS2SAP.API/Services/Implementations/PosDataService.cs](backend/POS2SAP.API/Services/Implementations/PosDataService.cs)
- New UI page with Query + table → [frontend/pos2sap-ui/src/pages/MonitorPage.tsx](frontend/pos2sap-ui/src/pages/MonitorPage.tsx)
- JSON detail layout → [frontend/pos2sap-ui/src/pages/MonitorDetailPage.tsx](frontend/pos2sap-ui/src/pages/MonitorDetailPage.tsx)
- Multi-step workflow UI (filter → preview → confirm → execute) → [frontend/pos2sap-ui/src/pages/ImportPage.tsx](frontend/pos2sap-ui/src/pages/ImportPage.tsx)
