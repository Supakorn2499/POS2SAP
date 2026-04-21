# 🔧 Database Setup Instructions

## Initialize Test Users

Before running the application, you need to seed test users into the `staffs` table with bcrypt-hashed passwords.

### Step 1: Run SQL Seed Script

Execute the following SQL script on your **HQ_FAMTIME** database:

```bash
sqlcmd -S your-server-name -d HQ_FAMTIME -i backend/POS2SAP.API/sql/seed-staffs.sql
```

Or use SQL Server Management Studio (SSMS) and run the script directly:
- File: `backend/POS2SAP.API/sql/seed-staffs.sql`

### Step 2: Test Users Created

After running the seed script, the following test users will be available:

| Username | Password     | Role        |
|----------|--------------|-------------|
| `admin`  | `Password@123` | Administrator |
| `user1`  | `User1@123`    | Regular User |

### Step 3: Restart Backend

After seeding, restart the backend server:

```bash
cd backend/POS2SAP.API
dotnet run
```

### Step 4: Test Login

1. Navigate to `http://localhost:5174` (frontend)
2. Login with:
   - Username: `admin`
   - Password: `Password@123`

## Troubleshooting

### Error: "Invalid salt version"

This error occurs when the BCrypt hash format is incorrect in the database.

**Solution:** Run the seed script above to update the password hashes.

### Error: "Unauthorized"

This error means the JWT token is not being sent properly.

**Solution:** 
1. Ensure you're logged in (check if token is in localStorage)
2. Clear browser cache: `localStorage.clear()`
3. Restart the frontend: `npm run dev`

### Database Connection Failed

Ensure the connection string in `appsettings.json` is correct:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=HQ_FAMTIME;User Id=YOUR_USER;Password=YOUR_PASSWORD;"
  }
}
```

## Generate New BCrypt Hashes

If you need to create additional test users, use the following to generate bcrypt hashes:

### Using .NET (C#)

```csharp
using BCrypt.Net;

string password = "YourPassword@123";
string hash = BCrypt.Net.BCrypt.HashPassword(password, 12);
Console.WriteLine(hash);
```

### Using Online Tool

Visit: https://bcrypt-generator.com/ (for development only)

Then add to `seed-staffs.sql`:

```sql
INSERT INTO staffs (StaffLogin, StaffPassword, StaffFirstName, StaffLastName)
VALUES ('newuser', 'YOUR_BCRYPT_HASH_HERE', 'First', 'Last');
```
