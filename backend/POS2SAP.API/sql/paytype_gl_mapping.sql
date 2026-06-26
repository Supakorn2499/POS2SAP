-- ============================================================
-- paytype_gl_mapping — GL account mapping for Incoming Payment
-- รันบน HQ_FAMTIME หลังจาก init.sql
-- ============================================================
-- SapPayCategory values (configured in GL Mapping UI):
--   CASH        → CashAcct + CashSum (sum all pay rows in this category)
--   TRANSFER    → TrsfrAcct + TrsfrSum + TrsfrDate + TrsfrRef (sum all rows)
--   CREDIT_CARD → paymentCreditCards[] one line per pay row
--   SKIP        → excluded from Incoming Payment JSON
-- ============================================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='paytype_gl_mapping' AND xtype='U')
BEGIN
    CREATE TABLE paytype_gl_mapping (
        MappingID       INT IDENTITY(1,1) PRIMARY KEY,
        PayTypeID       INT           NOT NULL,
        PayTypeName     NVARCHAR(100) NULL,          -- copy จาก paytype สำหรับ reference
        SapPayCategory  NVARCHAR(20)  NOT NULL DEFAULT 'SKIP',  -- CASH | TRANSFER | CREDIT_CARD | SKIP
        SapGlAccount    NVARCHAR(50)  NULL,          -- รหัส GL บัญชี SAP (ใส่หลังได้รับ mapping จาก SAP)
        SapPayTypeName  NVARCHAR(100) NULL,          -- ชื่อที่ใช้ใน SAP paymentCreditCards.CreditCard field
        IsActive        TINYINT       NOT NULL DEFAULT 1,
        SortOrder       INT           NOT NULL DEFAULT 0,
        Remarks         NVARCHAR(200) NULL,
        CreatedAt       DATETIME      NOT NULL DEFAULT GETDATE(),
        UpdatedAt       DATETIME      NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_paytype_gl_PayTypeID UNIQUE (PayTypeID)
    );

    PRINT 'Created table: paytype_gl_mapping';
END
ELSE
    PRINT 'Table paytype_gl_mapping already exists — skipping CREATE';

-- ============================================================
-- Seed data — SapGlAccount ใส่เป็น placeholder '[GL-PENDING]'
-- ให้แก้ไข SapGlAccount ให้ตรงกับ GL บัญชีจริงของ SAP
-- ============================================================

-- ลบ seed เก่าถ้ามีแล้ว insert ใหม่ (idempotent)
DELETE FROM paytype_gl_mapping;
DBCC CHECKIDENT ('paytype_gl_mapping', RESEED, 0);

INSERT INTO paytype_gl_mapping
    (PayTypeID, PayTypeName, SapPayCategory, SapGlAccount, SapPayTypeName, IsActive, SortOrder, Remarks)
VALUES
-- -----------------------------------------------------------
-- CASH
-- -----------------------------------------------------------
(1,   'Cash',                     'CASH',         '[GL-PENDING]', 'Cash',                     1, 10, 'เงินสด'),

-- -----------------------------------------------------------
-- CREDIT_CARD — บัตรเครดิต (พักบัญชีรอเคลียร์)
-- -----------------------------------------------------------
(2,   'Credit Card',              'CREDIT_CARD',  '[GL-PENDING]', 'พักบัตรเครดิตรอเคลียร์',  1, 20, 'บัตรเครดิตทุกธนาคาร'),

-- -----------------------------------------------------------
-- CREDIT_CARD — แพลตฟอร์ม Delivery (ใช้บัญชีพักรอรับเงินจากแพลตฟอร์ม)
-- -----------------------------------------------------------
(148, 'Grabfood Payment',         'CREDIT_CARD',  '[GL-PENDING]', 'Grabfood',                 1, 30, 'ยอดขาย Grabfood'),
(149, 'Robinhood Payment',        'CREDIT_CARD',  '[GL-PENDING]', 'Robinhood',                1, 31, 'ยอดขาย Robinhood'),
(150, 'FoodPanda Payment',        'CREDIT_CARD',  '[GL-PENDING]', 'FoodPanda',                1, 32, 'ยอดขาย FoodPanda'),
(151, 'Lineman Payment',          'CREDIT_CARD',  '[GL-PENDING]', 'Lineman',                  1, 33, 'ยอดขาย Lineman'),

-- -----------------------------------------------------------
-- CREDIT_CARD — Voucher (แต่ละประเภทอาจใช้ GL ต่างกัน)
-- -----------------------------------------------------------
(152, 'Voucher One Bangkok',      'CREDIT_CARD',  '[GL-PENDING]', 'Voucher One Bangkok',      1, 40, 'Voucher One Bangkok'),
(153, 'MEGA VOUCHER 500',         'CREDIT_CARD',  '[GL-PENDING]', 'MEGA VOUCHER 500',         1, 41, 'Voucher มูลค่า 500'),
(154, 'MEGA VOUCHER 100',         'CREDIT_CARD',  '[GL-PENDING]', 'MEGA VOUCHER 100',         1, 42, 'Voucher มูลค่า 100'),

-- -----------------------------------------------------------
-- SKIP — ไม่ส่ง SAP
-- -----------------------------------------------------------
(143, 'Compliment',               'SKIP',         NULL,           NULL,                       1, 99, 'Compliment — ไม่มียอดเงิน'),
(155, 'ตัดสต๊อก สินค้าหมดอายุ',     'SKIP',         NULL,           NULL,                       1, 99, 'Stock write-off'),
(156, 'ตัดสต๊อก สินค้าเสียหาย จากลูกค้า', 'SKIP', NULL,           NULL,                       1, 99, 'Stock write-off'),
(157, 'ตัดสต๊อก สินค้าเสียหาย จากพนักงาน','SKIP',NULL,           NULL,                       1, 99, 'Stock write-off'),
(158, 'ตัดสต๊อก สินค้าเสียหาย จากครัว', 'SKIP',  NULL,           NULL,                       1, 99, 'Stock write-off'),
(159, 'ตัดสต๊อก สินค้าเสียหาย จากบาร์',  'SKIP',  NULL,           NULL,                       1, 99, 'Stock write-off');

-- -----------------------------------------------------------
-- TRANSFER — เตรียมไว้สำหรับ PayType โอนเงินธนาคาร/PromptPay
--            (ปัจจุบัน IsAvailable=0 ใน paytype — เพิ่มเมื่อใช้งาน)
-- -----------------------------------------------------------
-- (138, 'Online Prompt Pay', 'TRANSFER', '[GL-PENDING]', 'Prompt Pay', 0, 50, 'PromptPay — ยังไม่เปิดใช้งาน')

PRINT 'Seeded paytype_gl_mapping — *** กรุณาอัปเดต SapGlAccount จาก [GL-PENDING] เป็นรหัส GL จริง ***';

-- ============================================================
-- Seed interface_configs สำหรับ IncomingPayment
-- key naming: IncomingPayment.<key> จะ override global key
-- ============================================================
-- ตรวจสอบก่อนว่ามีหรือยัง
IF NOT EXISTS (SELECT 1 FROM interface_configs WHERE config_key = 'IncomingPayment.sap_url_test')
BEGIN
    INSERT INTO interface_configs (id, config_key, config_value, description) VALUES
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'IncomingPayment.sap_url_test',  'http://999.999.999.999/TST/api/IncomingPayment', 'SAP Incoming Payment Test URL'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'IncomingPayment.sap_url_prod',  'http://999.999.999.999/PRD/api/IncomingPayment', 'SAP Incoming Payment Production URL'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'IncomingPayment.sap_env',       'TST',    'SAP env for IncomingPayment: TST or PRD'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'IncomingPayment.sap_auth_type', 'ApiKey', 'SAP auth type for IncomingPayment'),
        (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), 'IncomingPayment.sap_api_key',   '',       'SAP API Key for IncomingPayment');

    PRINT 'Inserted interface_configs for IncomingPayment';
END
ELSE
    PRINT 'interface_configs for IncomingPayment already exists — skipped';
