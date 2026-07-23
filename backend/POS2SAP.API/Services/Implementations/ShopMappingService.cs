using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Config;
using POS2SAP.API.Services.Interfaces;

namespace POS2SAP.API.Services.Implementations;

public class ShopMappingService : IShopMappingService
{
    private static bool _schemaReady;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);

    private readonly ILogger<ShopMappingService> _logger;

    public ShopMappingService(ILogger<ShopMappingService> logger)
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
                END";

            const string seedSql = @"
                INSERT INTO shop_sap_mapping
                    (ShopID, ShopCode, ShopName, SapCardCode, SapBranchCode, SapBranchName, SapVatBranch,
                     IsActive, SortOrder, Remarks)
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
                )";

            const string syncNamesSql = @"
                UPDATE m SET
                    ShopCode = ISNULL(sd.shopcode, m.ShopCode),
                    ShopName = ISNULL(sd.shopname, m.ShopName),
                    UpdatedAt = GETDATE()
                FROM shop_sap_mapping m
                INNER JOIN shop_data sd ON sd.ShopID = m.ShopID";

            await using var conn = new SqlConnection(gbVar.MainConstr);
            await conn.OpenAsync();
            await conn.ExecuteAsync(createSql);
            var seeded = await conn.ExecuteAsync(seedSql);
            var synced = await conn.ExecuteAsync(syncNamesSql);
            _schemaReady = true;
            _logger.LogInformation(
                "shop_sap_mapping ensured (seeded {Seeded}, synced names {Synced})",
                seeded, synced);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure shop_sap_mapping schema");
            throw;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private async Task EnsureReadyAsync()
    {
        if (!_schemaReady)
            await EnsureSchemaAsync();
    }

    public async Task<List<ShopSapMappingDto>> GetAllMappingsAsync()
    {
        const string sql = @"
            SELECT
                m.MappingID,
                m.ShopID,
                ISNULL(sd.shopcode, m.ShopCode) AS ShopCode,
                ISNULL(sd.shopname, m.ShopName) AS ShopName,
                ISNULL(sd.SLOC, '') AS PosSloc,
                ISNULL(NULLIF(sd.PTTShopCode, ''), ISNULL(sd.shopcode, '')) AS PosBranchCode,
                ISNULL(sd.BranchNo, '') AS PosVatBranch,
                m.SapCardCode,
                m.SapBranchCode,
                m.SapBranchName,
                m.SapVatBranch,
                CAST(m.IsActive AS BIT) AS IsActive,
                m.SortOrder,
                m.Remarks,
                m.UpdatedAt
            FROM shop_sap_mapping m
            INNER JOIN shop_data sd ON sd.ShopID = m.ShopID
            ORDER BY m.SortOrder, m.ShopID";

        await using var conn = new SqlConnection(gbVar.MainConstr);
        await conn.OpenAsync();
        await EnsureReadyAsync();
        return (await conn.QueryAsync<ShopSapMappingDto>(sql)).ToList();
    }

    public async Task<List<UnmappedShopDto>> GetUnmappedShopsAsync()
    {
        const string sql = @"
            SELECT
                sd.ShopID,
                ISNULL(sd.shopcode, CAST(sd.ShopID AS NVARCHAR(20))) AS ShopCode,
                ISNULL(sd.shopname, '') AS ShopName
            FROM shop_data sd
            WHERE NOT EXISTS (
                SELECT 1 FROM shop_sap_mapping m WHERE m.ShopID = sd.ShopID
            )
            ORDER BY sd.shopname, sd.ShopID";

        await using var conn = new SqlConnection(gbVar.MainConstr);
        await conn.OpenAsync();
        await EnsureReadyAsync();
        return (await conn.QueryAsync<UnmappedShopDto>(sql)).ToList();
    }

    public async Task<bool> UpsertMappingAsync(UpsertShopMappingDto dto)
    {
        const string sql = @"
            IF EXISTS (SELECT 1 FROM shop_sap_mapping WHERE ShopID = @ShopID)
            BEGIN
                UPDATE shop_sap_mapping SET
                    ShopCode      = @ShopCode,
                    ShopName      = @ShopName,
                    SapCardCode   = @SapCardCode,
                    SapBranchCode = @SapBranchCode,
                    SapBranchName = @SapBranchName,
                    SapVatBranch  = @SapVatBranch,
                    IsActive      = @IsActive,
                    SortOrder     = @SortOrder,
                    Remarks       = @Remarks,
                    UpdatedAt     = GETDATE()
                WHERE ShopID = @ShopID
            END
            ELSE
            BEGIN
                INSERT INTO shop_sap_mapping
                    (ShopID, ShopCode, ShopName, SapCardCode, SapBranchCode, SapBranchName, SapVatBranch,
                     IsActive, SortOrder, Remarks)
                VALUES
                    (@ShopID, @ShopCode, @ShopName, @SapCardCode, @SapBranchCode, @SapBranchName, @SapVatBranch,
                     @IsActive, @SortOrder, @Remarks)
            END";

        await using var conn = new SqlConnection(gbVar.MainConstr);
        await conn.OpenAsync();
        await EnsureReadyAsync();
        var affected = await conn.ExecuteAsync(sql, new
        {
            dto.ShopID,
            dto.ShopCode,
            dto.ShopName,
            dto.SapCardCode,
            dto.SapBranchCode,
            dto.SapBranchName,
            dto.SapVatBranch,
            IsActive = dto.IsActive ? 1 : 0,
            dto.SortOrder,
            dto.Remarks
        });
        return affected > 0;
    }

    public async Task<bool> DeleteMappingAsync(int shopId)
    {
        const string sql = "DELETE FROM shop_sap_mapping WHERE ShopID = @ShopID";
        await using var conn = new SqlConnection(gbVar.MainConstr);
        await conn.OpenAsync();
        await EnsureReadyAsync();
        return await conn.ExecuteAsync(sql, new { ShopID = shopId }) > 0;
    }
}
