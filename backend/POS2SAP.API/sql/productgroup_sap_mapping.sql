-- ============================================================
-- productgroup_sap_mapping — POS product group → SAP item group
-- รันบน HQ_FAMTIME หลังจาก init.sql
-- ============================================================
-- Maps productgroup.ProductGroupID → SapItemGroupCode (sent as ItemCategory in AR Invoice lines)
-- Placeholder [SAP-PENDING] = ยังไม่ได้กำหนดรหัส SAP จริง
-- ============================================================

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

    PRINT 'Created table: productgroup_sap_mapping';
END
ELSE
    PRINT 'Table productgroup_sap_mapping already exists — skipping CREATE';

-- Seed all POS product groups that are not yet mapped (idempotent insert)
INSERT INTO productgroup_sap_mapping
    (ProductGroupID, ProductGroupCode, ProductGroupName, SapItemGroupCode, SapItemGroupName, IsActive, SortOrder, Remarks)
SELECT
    pg.ProductGroupID,
    ISNULL(pg.ProductGroupCode, CAST(pg.ProductGroupID AS NVARCHAR(20))),
    ISNULL(
        NULLIF(LTRIM(RTRIM(ISNULL(pg.ProductGroupName, ''))), ''),
        ISNULL(pg.ProductGroupCode, CAST(pg.ProductGroupID AS NVARCHAR(20)))
    ),
    '[SAP-PENDING]',
    NULL,
    1,
    pg.ProductGroupID,
    N'Auto-seeded — update SapItemGroupCode from GL Mapping UI'
FROM productgroup pg
WHERE ISNULL(pg.Deleted, 0) = 0
  AND NOT EXISTS (
    SELECT 1 FROM productgroup_sap_mapping m WHERE m.ProductGroupID = pg.ProductGroupID
);

PRINT 'Seeded new product groups into productgroup_sap_mapping (existing rows untouched)';
