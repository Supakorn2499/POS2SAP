# Database Setup Instructions

## Staff login (use existing POS users)

POS2SAP shares the **`staffs`** table with the POS system on `HQ_FAMTIME`.

**Do not INSERT new rows into `staffs` from this project.**  
`StaffID` is **not** an identity column — inserts without `StaffID` can create invalid rows with `StaffID = 0`.

Login flow (`AuthController`) only:

1. `SELECT` from `staffs` by `StaffLogin`
2. Verify password (BCrypt or legacy SHA1)
3. Optionally `UPDATE StaffPassword` when upgrading SHA1 → BCrypt

There is **no** code path that creates staff on login.

### Use an existing account

Example: `StaffLogin = 'vtec'` with your POS password.

Requirements:

- `Activated = 1`
- `Deleted = 0`
- Valid `StaffPassword` (BCrypt or legacy SHA1)

### Remove accidental test rows

If `seed-staffs.sql` was run on a shared database in the past, you may have test users with `StaffID = 0`:

```sql
-- Review first
SELECT StaffID, StaffLogin, StaffFirstName FROM staffs WHERE StaffID = 0;

-- Remove only test logins (adjust list as needed)
DELETE FROM staffs WHERE StaffID = 0 AND StaffLogin IN ('admin', 'user1');
```

## Auth schema (POS2SAP tables only)

Run on `HQ_FAMTIME` to ensure `refresh_tokens` and `staffs.UpdatedAt` exist:

```bash
sqlcmd -S your-server-name -d HQ_FAMTIME -i backend/POS2SAP.API/sql/ensure_auth_schema.sql
```

Or: `backend/POS2SAP.API/scripts/run_db_setup.ps1`

Main interface tables: `backend/POS2SAP.API/sql/init.sql`

## Troubleshooting

### "Invalid salt version"

Password hash in `staffs` may be corrupt. Re-hash with BCrypt for that existing row only (do not insert a new staff row).

### "Unauthorized" after login

1. Check JWT in browser `localStorage['pos2sapToken']`
2. Ensure `Jwt:Secret` is set in `appsettings.Development.json` or `JWT__Secret` env var

### Generate BCrypt for an existing user (UPDATE only)

```csharp
using BCrypt.Net;
string hash = BCrypt.Net.BCrypt.HashPassword("your-password", 11);
```

```sql
UPDATE staffs SET StaffPassword = @hash, UpdatedAt = GETDATE() WHERE StaffLogin = 'vtec';
```
