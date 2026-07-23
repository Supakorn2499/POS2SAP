-- ============================================================
-- shop_sap_mapping — POS shop_data → SAP branch / card / VAT fields
-- รันบน HQ_FAMTIME (หรือรอ EnsureSchema ตอน API startup)
-- ============================================================
-- Overrides used by AR / AP / DL:
--   SapCardCode   → CardCode   (fallback shop_data.SLOC)
--   SapBranchCode → BranchCode (fallback PTTShopCode / shopcode)
--   SapBranchName → BranchName / CardName (fallback shopname)
--   SapVatBranch  → VatBranch  (fallback BranchNo)
-- ============================================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='shop_sap_mapping' AND xtype='U')
BEGIN
    CREATE TABLE shop_sap_mapping (
        MappingID      INT IDENTITY(1,1) PRIMARY KEY,
        ShopID         INT           NOT NULL,
        ShopCode       NVARCHAR(50)  NULL,
        ShopName       NVARCHAR(200) NULL,
        SapCardCode    NVARCHAR(50)  NULL,
        SapBranchCode  NVARCHAR(50)  NULL,
        SapBranchName  NVARCHAR(200) NULL,
        SapVatBranch   NVARCHAR(50)  NULL,
        IsActive       TINYINT       NOT NULL DEFAULT 1,
        SortOrder      INT           NOT NULL DEFAULT 0,
        Remarks        NVARCHAR(200) NULL,
        CreatedAt      DATETIME      NOT NULL DEFAULT GETDATE(),
        UpdatedAt      DATETIME      NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_shop_sap_ShopID UNIQUE (ShopID)
    );

    PRINT 'Created table: shop_sap_mapping';
END
ELSE
    PRINT 'Table shop_sap_mapping already exists — skipping CREATE';

-- Seed from shop_data (copy current SAP-related fields so behavior stays the same until edited)
INSERT INTO shop_sap_mapping
    (ShopID, ShopCode, ShopName, SapCardCode, SapBranchCode, SapBranchName, SapVatBranch, IsActive, SortOrder, Remarks)
SELECT
    sd.ShopID,
    ISNULL(sd.shopcode, CAST(sd.ShopID AS NVARCHAR(20))),
    ISNULL(sd.shopname, ''),
    ISNULL(sd.SLOC, ''),
    ISNULL(NULLIF(sd.PTTShopCode, ''), sd.shopcode),
    ISNULL(sd.shopname, ''),
    ISNULL(sd.BranchNo, ''),
    1,
    sd.ShopID,
    N'Auto-seeded from shop_data — edit in Shop Mapping UI'
FROM shop_data sd
WHERE NOT EXISTS (
    SELECT 1 FROM shop_sap_mapping m WHERE m.ShopID = sd.ShopID
);

PRINT 'Seeded new shops into shop_sap_mapping (existing rows untouched)';
