# POS2SAP Security Setup - Phase 1

## Changes Made

### 1. **Password Hashing: SHA1 → BCrypt**
- Old: SHA1 (vulnerable to rainbow tables)
- New: BCrypt with workFactor=11 (industry standard)
- All new login attempts use BCrypt verification

### 2. **JWT Authentication**
- Access tokens expire in 60 minutes (configurable)
- Refresh tokens expire in 7 days (configurable)
- Tokens stored in database for revocation support
- Endpoints:
  - `POST /api/auth/login` - Returns access token + refresh token
  - `POST /api/auth/refresh` - Renew expired access token

### 3. **JWT Middleware & Authorization**
- `JwtAuthMiddleware` - Extracts & validates bearer tokens
- `AuthorizationMiddleware` - Checks authentication on protected routes
- Public routes (no auth required):
  - `/api/auth/login`
  - `/api/auth/refresh`
  - `/swagger/*`
  - `/health`

### 4. **[Authorize] Attributes**
Applied to protected controllers:
- `MonitorController` - All endpoints
- `ConfigController` - All endpoints  
- `InterfaceController` - All endpoints

AuthController is public (login doesn't require auth)

### 5. **Improved CORS**
- Specific origin whitelisting
- Added credential support
- Exposed Authorization header
- Allowed: GET, POST, PUT, DELETE, OPTIONS

### 6. **Rate Limiting**
- General limit: 100 requests/minute per IP
- Ready for specific limits per endpoint
- Implemented via AspNetCoreRateLimit

### 7. **Database Changes**
Created `refresh_tokens` table:
```sql
CREATE TABLE refresh_tokens (
    id              INT PRIMARY KEY IDENTITY(1,1),
    staff_login     NVARCHAR(100) UNIQUE,
    token           NVARCHAR(MAX),
    expires_at      DATETIME2,
    created_at      DATETIME2,
    updated_at      DATETIME2
)
```

## Configuration

### appsettings.json
```json
{
  "Jwt": {
    "Secret": "your-very-long-secret-key-at-least-32-characters",
    "ExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7,
    "Issuer": "POS2SAP",
    "Audience": "POS2SAPUsers"
  },
  "RateLimit": {
    "GeneralLimit": {
      "Period": "1m",
      "Limit": 100
    }
  }
}
```

**⚠️ IMPORTANT**: Change the JWT Secret to a secure random string before production!
```bash
# Generate a secure secret (PowerShell)
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```

## Setup Steps

### 1. Restore NuGet Packages
```bash
cd backend/POS2SAP.API
dotnet restore
```

### 2. Run Database Migration
Execute `backend/POS2SAP.API/sql/init.sql` on your SQL Server:
```sql
-- This creates the refresh_tokens table
```

### 3. Configure JWT Secret
Update `appsettings.json` with a secure secret key (32+ characters)

### 4. Build & Run
```bash
dotnet build
dotnet run
```

## Usage

### Login Flow
```bash
# 1. Login
curl -X POST https://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"staffLogin":"user123","staffPassword":"password123"}'

# Response:
{
  "data": {
    "staffLogin": "user123",
    "staffFirstName": "John",
    "staffLastName": "Doe",
    "accessToken": "eyJhbGc...",
    "refreshToken": "base64token...",
    "expiresIn": 3600
  }
}

# 2. Use access token for protected endpoints
curl -X GET https://localhost:5000/api/monitor/logs \
  -H "Authorization: Bearer eyJhbGc..."

# 3. Refresh token when expired
curl -X POST https://localhost:5000/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"base64token..."}'
```

## Security Checklist

- [ ] Change JWT Secret in appsettings.json
- [ ] Run database migration (refresh_tokens table)
- [ ] Restore NuGet packages (BCrypt, JWT)
- [ ] Test login endpoint
- [ ] Verify protected endpoints require token
- [ ] Test token refresh
- [ ] Configure HTTPS in production
- [ ] Add rate limiting per endpoint if needed

## Next Steps (Phase 2)

1. Input Validation (FluentValidation)
2. Custom Exception Handling
3. Unit Tests
4. Frontend JWT integration
5. Additional role-based authorization

---

**Created**: April 20, 2026  
**Phase**: 1 - Security Foundation
