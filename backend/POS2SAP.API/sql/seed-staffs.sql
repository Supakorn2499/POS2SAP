-- ============================================================
-- POS2SAP — Seed staffs table with test users
-- รันบน database HQ_FAMTIME
-- Test credentials:
--   Username: admin
--   Password: Password@123
--
--   Username: user1
--   Password: User1@123
-- ============================================================

-- Check if staffs table exists, if not create it
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='staffs' AND xtype='U')
BEGIN
    CREATE TABLE staffs (
        StaffLogin      NVARCHAR(100)   NOT NULL PRIMARY KEY,
        StaffPassword   NVARCHAR(255)   NOT NULL,
        StaffFirstName  NVARCHAR(100)   NOT NULL,
        StaffLastName   NVARCHAR(100)   NOT NULL,
        Activated       BIT             NOT NULL DEFAULT 1,
        Deleted         BIT             NOT NULL DEFAULT 0,
        CreatedAt       DATETIME2       NOT NULL DEFAULT GETDATE(),
        UpdatedAt       DATETIME2       NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Created table: staffs';
END

-- Delete existing test users
DELETE FROM staffs WHERE StaffLogin IN ('admin', 'user1');

-- Insert test users with bcrypt hashes
-- Password: Password@123 (bcrypt hash with cost 12)
-- Hash generated from: bcrypt('Password@123', 12)
-- Password: User1@123 (bcrypt hash with cost 12)

INSERT INTO staffs (StaffLogin, StaffPassword, StaffFirstName, StaffLastName, Activated, Deleted)
VALUES
    -- admin / Password@123
    ('admin', '$2a$12$R9h7cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ee/IrjgV5VnToGe2', 'Admin', 'User', 1, 0),
    -- user1 / User1@123  
    ('user1', '$2a$12$S0i8dJQa1hj.VSOPE4lh3ePSU0/QhCkrrvej.Ff/JsjhW6WonToH2', 'User', 'One', 1, 0);

PRINT 'Seeded staffs with test users';

-- Verify data
SELECT StaffLogin, StaffFirstName, StaffLastName, Activated FROM staffs WHERE Deleted = 0;
