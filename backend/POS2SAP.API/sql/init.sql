-- ============================================================
-- POS2SAP — Init SQL Script
-- รันบน database HQ_FAMTIME ครั้งเดียว
-- ============================================================

-- --------------------------------------------------------
-- Table: interface_logs
-- --------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='interface_logs' AND xtype='U')
BEGIN
    CREATE TABLE interface_logs (
        id               NVARCHAR(26)     NOT NULL PRIMARY KEY,  -- ULID
        pos_doc_no       NVARCHAR(30)     NOT NULL,
        pos_doc_date     DATE             NULL,
        branch_code      NVARCHAR(15)     NULL,
        branch_name      NVARCHAR(100)    NULL,
        pos_id           NVARCHAR(20)     NULL,
        card_code        NVARCHAR(15)     NULL,
        channel          NVARCHAR(100)    NULL,
        interface_type   NVARCHAR(20)     NOT NULL DEFAULT 'AR',
        doc_total        DECIMAL(19,6)    NULL,
        pos_data         NVARCHAR(MAX)    NULL,   -- JSON snapshot ข้อมูล POS ต้นทาง
        sap_doc_num      NVARCHAR(30)     NULL,   -- DocNum ที่ SAP ตอบกลับมา
        sap_request      NVARCHAR(MAX)    NULL,   -- JSON ที่ส่งไป SAP
        sap_response     NVARCHAR(MAX)    NULL,   -- JSON ที่ SAP ตอบกลับ
        status           NVARCHAR(20)     NOT NULL DEFAULT 'PENDING',  -- PENDING/PROCESSING/SUCCESS/FAILED/RETRY
        error_message    NVARCHAR(MAX)    NULL,
        retry_count      INT              NOT NULL DEFAULT 0,
        sent_at          DATETIME2        NULL,
        is_deleted       BIT              NOT NULL DEFAULT 0,
        created_at       DATETIME2        NOT NULL DEFAULT GETDATE(),
        created_by       NVARCHAR(100)    NULL,
        updated_at       DATETIME2        NOT NULL DEFAULT GETDATE(),
        updated_by       NVARCHAR(100)    NULL
    );

    CREATE INDEX IX_interface_logs_status       ON interface_logs (status);
    CREATE INDEX IX_interface_logs_pos_doc_no   ON interface_logs (pos_doc_no);
    CREATE INDEX IX_interface_logs_branch_code  ON interface_logs (branch_code);
    CREATE INDEX IX_interface_logs_interface_type  ON interface_logs (interface_type);
    CREATE INDEX IX_interface_logs_created_at   ON interface_logs (created_at);

    PRINT 'Created table: interface_logs';
END
ELSE
    PRINT 'Table already exists: interface_logs';

-- --------------------------------------------------------
-- Table: interface_configs
-- --------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='interface_configs' AND xtype='U')
BEGIN
    CREATE TABLE interface_configs (
        id              NVARCHAR(26)     NOT NULL PRIMARY KEY,  -- ULID
        config_key      NVARCHAR(100)    NOT NULL UNIQUE,
        config_value    NVARCHAR(500)    NULL,
        description     NVARCHAR(255)    NULL,
        is_active       BIT              NOT NULL DEFAULT 1,
        created_at      DATETIME2        NOT NULL DEFAULT GETDATE(),
        updated_at      DATETIME2        NOT NULL DEFAULT GETDATE()
    );

    -- Seed default config values
    INSERT INTO interface_configs (id, config_key, config_value, description) VALUES
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'sap_url_test',               'http://999.999.999.999/TST/api', 'SAP B1 Test Environment URL'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'sap_url_prod',               'http://999.999.999.999/PRD/api', 'SAP B1 Production Environment URL'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'sap_env',                    'TST',    'Active SAP environment: TST or PRD'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'sap_auth_type',              'ApiKey', 'SAP auth type: None / ApiKey / Basic'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'sap_api_key',                '',       'SAP API Key (if auth_type = ApiKey)'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'sap_basic_username',         '',       'SAP Basic Auth username (if auth_type = Basic)'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'sap_basic_password',         '',       'SAP Basic Auth password (if auth_type = Basic)'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'schedule_interval_minutes',  '5',      'Interval in minutes for scheduled job wake-up'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'schedule_enabled',           'true',   'Enable/disable automatic scheduled job'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'schedule_window_start',      '20:00',  'Daily start time (HH:mm) — empty = always'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'schedule_window_end',        '06:00',  'Daily end time (HH:mm) — overnight OK; empty = no end'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'schedule_timezone',          'Asia/Bangkok', 'IANA or Windows timezone id'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'schedule_max_runtime_minutes','240',   'Max continuous drain minutes per wake-up'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'interface_cutover_date',     '2026-06-01', 'First POS doc date to interface (yyyy-MM-dd)'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'import_date_to_mode',        'yesterday', 'Import up to: yesterday or today'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'import_batch_size',          '500',    'Max docs per import/send batch'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'sap_http_timeout_seconds',   '90',     'SAP HTTP timeout per request (seconds)'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'import_chunk_days',          '7',      'Split import queries by N-day chunks when range is wider'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'max_retry_count',            '3',      'Maximum retry attempts before marking FAILED final');

    PRINT 'Created table: interface_configs with seed data';
END
ELSE
    PRINT 'Table already exists: interface_configs';

-- --------------------------------------------------------
-- Seed sample interface_logs for dashboard preview
-- --------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM interface_logs WHERE pos_doc_no = 'AR-0001')
BEGIN
    INSERT INTO interface_logs
        (id, pos_doc_no, pos_doc_date, branch_code, branch_name, pos_id, card_code, channel, interface_type,
         doc_total, pos_data, sap_doc_num, sap_request, sap_response, status, error_message, retry_count, sent_at,
         is_deleted, created_at, created_by, updated_at, updated_by)
    VALUES
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'AR-0001', '2026-04-10', '5',  'สาขา 5',  'POS-01', 'CUST001', 'POS', 'AR', 512.25, '{"doc":"AR-0001"}', NULL, '{"request":"sample"}', NULL, 'PENDING', NULL, 0, NULL, 0, DATEADD(DAY,-7,GETDATE()), 'seed', DATEADD(DAY,-7,GETDATE()), 'seed'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'AR-0002', '2026-04-11', '5',  'สาขา 5',  'POS-02', 'CUST002', 'POS', 'AR', 846.50, '{"doc":"AR-0002"}', NULL, '{"request":"sample"}', NULL, 'PROCESSING', NULL, 0, NULL, 0, DATEADD(DAY,-6,GETDATE()), 'seed', DATEADD(DAY,-6,GETDATE()), 'seed'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'AR-0003', '2026-04-12', '5',  'สาขา 5',  'POS-03', 'CUST003', 'POS', 'AR', 732.00, '{"doc":"AR-0003"}', 'SAP-1001', '{"request":"sample"}', '{"response":"ok"}', 'SUCCESS', NULL, 0, GETDATE(), 0, DATEADD(DAY,-5,GETDATE()), 'seed', DATEADD(DAY,-5,GETDATE()), 'seed'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'AR-0004', '2026-04-13', '6',  'สาขา 6',  'POS-04', 'CUST004', 'WEB', 'AR', 412.90, '{"doc":"AR-0004"}', NULL, '{"request":"sample"}', '{"error":"Invalid customer"}', 'FAILED', 'Customer not found', 2, GETDATE(), 0, DATEADD(DAY,-4,GETDATE()), 'seed', DATEADD(DAY,-4,GETDATE()), 'seed'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'AR-0005', '2026-04-14', '6',  'สาขา 6',  'POS-05', 'CUST005', 'POS', 'AR', 299.40, '{"doc":"AR-0005"}', NULL, '{"request":"sample"}', '{"error":"Timeout"}', 'RETRY', 'Timeout during SAP call', 1, GETDATE(), 0, DATEADD(DAY,-3,GETDATE()), 'seed', DATEADD(DAY,-3,GETDATE()), 'seed'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'AR-0006', '2026-04-15', '7',  'สาขา 7',  'POS-06', 'CUST006', 'WEB', 'AR', 620.10, '{"doc":"AR-0006"}', NULL, '{"request":"sample"}', '{"error":"Invalid item"}', 'FAILED', 'Invalid item code', 2, GETDATE(), 0, DATEADD(DAY,-2,GETDATE()), 'seed', DATEADD(DAY,-2,GETDATE()), 'seed'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'AR-0007', '2026-04-16', '11', 'สาขา 11', 'POS-07', 'CUST007', 'POS', 'AR', 407.80, '{"doc":"AR-0007"}', NULL, '{"request":"sample"}', NULL, 'PENDING', NULL, 0, NULL, 0, DATEADD(DAY,-1,GETDATE()), 'seed', DATEADD(DAY,-1,GETDATE()), 'seed'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'AR-0008', '2026-04-16', '11', 'สาขา 11', 'POS-08', 'CUST008', 'POS', 'AR', 526.90, '{"doc":"AR-0008"}', 'SAP-1002', '{"request":"sample"}', '{"response":"ok"}', 'SUCCESS', NULL, 0, GETDATE(), 0, DATEADD(DAY,-1,GETDATE()), 'seed', DATEADD(DAY,-1,GETDATE()), 'seed'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'AR-0009', '2026-04-16', '4',  'สาขา 4',  'POS-09', 'CUST009', 'WEB', 'AR', 289.00, '{"doc":"AR-0009"}', NULL, '{"request":"sample"}', '{"error":"SAP connection failed"}', 'FAILED', 'SAP connection failed', 1, GETDATE(), 0, DATEADD(DAY,-1,GETDATE()), 'seed', DATEADD(DAY,-1,GETDATE()), 'seed'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'AP-0001', '2026-04-16', '5',  'สาขา 5',  'POS-10', 'CUST010', 'POS', 'AP', 1200.00, '{"doc":"AP-0001"}', NULL, '{"request":"sample"}', NULL, 'PENDING', NULL, 0, NULL, 0, DATEADD(DAY,-1,GETDATE()), 'seed', DATEADD(DAY,-1,GETDATE()), 'seed'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'AP-0002', '2026-04-15', '7',  'สาขา 7',  'POS-11', 'CUST011', 'WEB', 'AP', 890.50, '{"doc":"AP-0002"}', NULL, '{"request":"sample"}', '{"error":"Vendor not found"}', 'FAILED', 'Vendor not found', 0, GETDATE(), 0, DATEADD(DAY,-2,GETDATE()), 'seed', DATEADD(DAY,-2,GETDATE()), 'seed');

    PRINT 'Seeded sample interface_logs for dashboard';
END

-- --------------------------------------------------------
-- Table: refresh_tokens
-- --------------------------------------------------------
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
    PRINT 'Table already exists: refresh_tokens';

-- --------------------------------------------------------
-- Table: paytype_gl_mapping  (Incoming Payment GL mapping)
-- ประกอบด้วยใน paytype_gl_mapping.sql — นำเข้ารันแยกต่างหาก
-- Included here only as CREATE stub (no seed — run paytype_gl_mapping.sql for seed)
-- --------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='paytype_gl_mapping' AND xtype='U')
BEGIN
    CREATE TABLE paytype_gl_mapping (
        MappingID       INT IDENTITY(1,1) PRIMARY KEY,
        PayTypeID       INT           NOT NULL,
        PayTypeName     NVARCHAR(100) NULL,
        SapPayCategory  NVARCHAR(20)  NOT NULL DEFAULT 'SKIP',
        SapGlAccount    NVARCHAR(50)  NULL,
        SapPayTypeName  NVARCHAR(100) NULL,
        IsActive        TINYINT       NOT NULL DEFAULT 1,
        SortOrder       INT           NOT NULL DEFAULT 0,
        Remarks         NVARCHAR(200) NULL,
        CreatedAt       DATETIME      NOT NULL DEFAULT GETDATE(),
        UpdatedAt       DATETIME      NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_paytype_gl_PayTypeID UNIQUE (PayTypeID)
    );

    PRINT 'Created table: paytype_gl_mapping — run paytype_gl_mapping.sql to seed GL accounts';
END
ELSE
    PRINT 'Table already exists: paytype_gl_mapping';

-- --------------------------------------------------------
-- Table: productgroup_sap_mapping  (POS product group → SAP item group)
-- Seed: run productgroup_sap_mapping.sql separately
-- --------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='productgroup_sap_mapping' AND xtype='U')
BEGIN
    CREATE TABLE productgroup_sap_mapping (
        MappingID          INT IDENTITY(1,1) PRIMARY KEY,
        ProductGroupID     INT           NOT NULL,
        ProductGroupCode   NVARCHAR(50)  NULL,
        ProductGroupName   NVARCHAR(100) NULL,
        SapItemGroupCode   NVARCHAR(50)  NULL,
        SapItemGroupName   NVARCHAR(100) NULL,
        IsActive           TINYINT       NOT NULL DEFAULT 1,
        SortOrder          INT           NOT NULL DEFAULT 0,
        Remarks            NVARCHAR(200) NULL,
        CreatedAt          DATETIME      NOT NULL DEFAULT GETDATE(),
        UpdatedAt          DATETIME      NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_productgroup_sap_ProductGroupID UNIQUE (ProductGroupID)
    );

    PRINT 'Created table: productgroup_sap_mapping — run productgroup_sap_mapping.sql to seed';
END
ELSE
    PRINT 'Table already exists: productgroup_sap_mapping';

-- --------------------------------------------------------
-- Table: dl_documenttype_mapping  (Delivery — enabled POS document types)
-- Seed: auto on API startup via DeliveryDocTypeService.EnsureSchemaAsync
-- --------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='dl_documenttype_mapping' AND xtype='U')
BEGIN
    CREATE TABLE dl_documenttype_mapping (
        MappingID          INT IDENTITY(1,1) PRIMARY KEY,
        DocumentTypeID     INT           NOT NULL,
        DocumentTypeCode   NVARCHAR(50)  NOT NULL,
        DocumentTypeName   NVARCHAR(200) NULL,
        IsEnabled          TINYINT       NOT NULL DEFAULT 1,
        SortOrder          INT           NOT NULL DEFAULT 0,
        Remarks            NVARCHAR(200) NULL,
        CreatedAt          DATETIME      NOT NULL DEFAULT GETDATE(),
        UpdatedAt          DATETIME      NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_dl_doctype_DocumentTypeID UNIQUE (DocumentTypeID)
    );

    PRINT 'Created table: dl_documenttype_mapping — run API once to seed default STOCK types';
END
ELSE
    PRINT 'Table already exists: dl_documenttype_mapping';

