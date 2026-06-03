-- Ensure auth-related schema exists: staffs UpdatedAt and refresh_tokens
-- Run this against the HQ_FAMTIME database

-- Add UpdatedAt to staffs if missing
IF COL_LENGTH('staffs', 'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE staffs
    ADD UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_staffs_UpdatedAt DEFAULT GETDATE();
    PRINT 'Added column UpdatedAt to staffs';
END
ELSE
    PRINT 'Column UpdatedAt already exists on staffs';

-- Create refresh_tokens table if missing
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='refresh_tokens' AND xtype='U')
BEGIN
    CREATE TABLE refresh_tokens (
        id              INT             NOT NULL PRIMARY KEY IDENTITY(1,1),
        staff_login     NVARCHAR(100)   NOT NULL UNIQUE,
        token           NVARCHAR(MAX)   NOT NULL,
        expires_at      DATETIME2       NOT NULL,
        created_at      DATETIME2       NOT NULL DEFAULT GETDATE(),
        updated_at      DATETIME2       NOT NULL DEFAULT GETDATE()
    );

    CREATE INDEX IX_refresh_tokens_staff_login ON refresh_tokens (staff_login);
    CREATE INDEX IX_refresh_tokens_expires_at  ON refresh_tokens (expires_at);

    PRINT 'Created table: refresh_tokens';
END
ELSE
    PRINT 'Table refresh_tokens already exists';
