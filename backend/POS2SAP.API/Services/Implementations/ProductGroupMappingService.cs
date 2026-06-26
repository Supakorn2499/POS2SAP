using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Config;
using POS2SAP.API.Services.Interfaces;

namespace POS2SAP.API.Services.Implementations;

public class ProductGroupMappingService : IProductGroupMappingService
{
    private static bool _schemaReady;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);

    private readonly ILogger<ProductGroupMappingService> _logger;

    public ProductGroupMappingService(ILogger<ProductGroupMappingService> logger)
    {
        _logger = logger;
    }

    public async Task EnsureSchemaAsync()
    {
        if (_schemaReady) return;

        await SchemaLock.WaitAsync();
        try
        {
            if (_schemaReady) return;

            const string createSql = @"
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
                END";

            const string seedSql = @"
                INSERT INTO productgroup_sap_mapping
                    (ProductGroupID, ProductGroupCode, ProductGroupName, SapItemGroupCode, SapItemGroupName, IsActive, SortOrder, Remarks)
                SELECT
                    pg.ProductGroupID,
                    ISNULL(pg.ProductGroupCode, CAST(pg.ProductGroupID AS NVARCHAR(20))),
                    ISNULL(
                        NULLIF(LTRIM(RTRIM(ISNULL(pg.ProductGroupName, ''))), ''),
                        ISNULL(pg.ProductGroupCode, CAST(pg.ProductGroupID AS NVARCHAR(20)))
                    ),
                    @PendingCode,
                    NULL,
                    1,
                    pg.ProductGroupID,
                    N'Auto-seeded — update SapItemGroupCode from Product Group Mapping UI'
                FROM productgroup pg
                WHERE ISNULL(pg.Deleted, 0) = 0
                  AND NOT EXISTS (
                    SELECT 1 FROM productgroup_sap_mapping m WHERE m.ProductGroupID = pg.ProductGroupID
                )";

            const string syncNamesSql = @"
                UPDATE m SET
                    ProductGroupCode = ISNULL(pg.ProductGroupCode, m.ProductGroupCode),
                    ProductGroupName = ISNULL(
                        NULLIF(LTRIM(RTRIM(ISNULL(pg.ProductGroupName, ''))), ''),
                        ISNULL(pg.ProductGroupCode, m.ProductGroupCode)
                    ),
                    UpdatedAt = GETDATE()
                FROM productgroup_sap_mapping m
                INNER JOIN productgroup pg
                    ON pg.ProductGroupID = m.ProductGroupID AND ISNULL(pg.Deleted, 0) = 0";

            await using var conn = new SqlConnection(gbVar.MainConstr);
            await conn.OpenAsync();
            await conn.ExecuteAsync(createSql);
            var seeded = await conn.ExecuteAsync(seedSql, new { PendingCode = gbVar.SapItemGroupPending });
            var synced = await conn.ExecuteAsync(syncNamesSql);
            _schemaReady = true;
            _logger.LogInformation(
                "productgroup_sap_mapping ensured (seeded {Seeded}, synced names {Synced})",
                seeded, synced);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure productgroup_sap_mapping schema");
            throw;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private async Task EnsureReadyAsync(IDbConnection conn)
    {
        if (!_schemaReady)
            await EnsureSchemaAsync();
    }

    public async Task<List<ProductGroupSapMappingDto>> GetAllMappingsAsync()
    {
        const string sql = @"
            SELECT
                m.MappingID,
                m.ProductGroupID,
                ISNULL(pg.ProductGroupCode, m.ProductGroupCode) AS ProductGroupCode,
                ISNULL(
                    NULLIF(LTRIM(RTRIM(ISNULL(pg.ProductGroupName, ''))), ''),
                    ISNULL(
                        NULLIF(LTRIM(RTRIM(ISNULL(m.ProductGroupName, ''))), ''),
                        ISNULL(pg.ProductGroupCode, m.ProductGroupCode)
                    )
                ) AS ProductGroupName,
                m.SapItemGroupCode,
                m.SapItemGroupName,
                CAST(m.IsActive AS BIT) AS IsActive,
                m.SortOrder,
                m.Remarks,
                m.UpdatedAt
            FROM productgroup_sap_mapping m
            INNER JOIN productgroup pg ON pg.ProductGroupID = m.ProductGroupID AND ISNULL(pg.Deleted, 0) = 0
            ORDER BY m.SortOrder, m.ProductGroupID";

        await using var conn = new SqlConnection(gbVar.MainConstr);
        await conn.OpenAsync();
        await EnsureReadyAsync(conn);
        var rows = await conn.QueryAsync<ProductGroupSapMappingDto>(sql);
        return rows.ToList();
    }

    public async Task<List<UnmappedProductGroupDto>> GetUnmappedProductGroupsAsync()
    {
        const string sql = @"
            SELECT
                pg.ProductGroupID,
                ISNULL(pg.ProductGroupCode, '') AS ProductGroupCode,
                ISNULL(
                    NULLIF(LTRIM(RTRIM(ISNULL(pg.ProductGroupName, ''))), ''),
                    ISNULL(pg.ProductGroupCode, CAST(pg.ProductGroupID AS NVARCHAR(20)))
                ) AS ProductGroupName
            FROM productgroup pg
            WHERE ISNULL(pg.Deleted, 0) = 0
              AND NOT EXISTS (
                SELECT 1 FROM productgroup_sap_mapping m WHERE m.ProductGroupID = pg.ProductGroupID
            )
            ORDER BY pg.ProductGroupID";

        await using var conn = new SqlConnection(gbVar.MainConstr);
        await conn.OpenAsync();
        await EnsureReadyAsync(conn);
        var rows = await conn.QueryAsync<UnmappedProductGroupDto>(sql);
        return rows.ToList();
    }

    public async Task<bool> UpsertMappingAsync(UpsertProductGroupMappingDto dto)
    {
        const string sql = @"
            IF EXISTS (SELECT 1 FROM productgroup_sap_mapping WHERE ProductGroupID = @ProductGroupID)
            BEGIN
                UPDATE productgroup_sap_mapping SET
                    ProductGroupCode   = @ProductGroupCode,
                    ProductGroupName   = @ProductGroupName,
                    SapItemGroupCode   = @SapItemGroupCode,
                    SapItemGroupName   = @SapItemGroupName,
                    IsActive           = @IsActive,
                    SortOrder          = @SortOrder,
                    Remarks            = @Remarks,
                    UpdatedAt          = GETDATE()
                WHERE ProductGroupID = @ProductGroupID
            END
            ELSE
            BEGIN
                INSERT INTO productgroup_sap_mapping
                    (ProductGroupID, ProductGroupCode, ProductGroupName,
                     SapItemGroupCode, SapItemGroupName, IsActive, SortOrder, Remarks)
                VALUES
                    (@ProductGroupID, @ProductGroupCode, @ProductGroupName,
                     @SapItemGroupCode, @SapItemGroupName, @IsActive, @SortOrder, @Remarks)
            END";

        await using var conn = new SqlConnection(gbVar.MainConstr);
        await conn.OpenAsync();
        await EnsureReadyAsync(conn);
        var affected = await conn.ExecuteAsync(sql, new
        {
            dto.ProductGroupID,
            dto.ProductGroupCode,
            dto.ProductGroupName,
            dto.SapItemGroupCode,
            dto.SapItemGroupName,
            IsActive = dto.IsActive ? 1 : 0,
            dto.SortOrder,
            dto.Remarks
        });
        return affected > 0;
    }

    public async Task<bool> DeleteMappingAsync(int productGroupId)
    {
        const string sql = "DELETE FROM productgroup_sap_mapping WHERE ProductGroupID = @ProductGroupID";
        await using var conn = new SqlConnection(gbVar.MainConstr);
        await conn.OpenAsync();
        await EnsureReadyAsync(conn);
        var affected = await conn.ExecuteAsync(sql, new { ProductGroupID = productGroupId });
        return affected > 0;
    }
}
