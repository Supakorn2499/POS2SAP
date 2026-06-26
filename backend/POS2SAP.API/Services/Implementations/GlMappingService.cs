using Dapper;
using Microsoft.Data.SqlClient;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Config;
using POS2SAP.API.Services.Interfaces;

namespace POS2SAP.API.Services.Implementations;

public class GlMappingService : IGlMappingService
{
    private readonly ILogger<GlMappingService> _logger;

    public GlMappingService(ILogger<GlMappingService> logger)
    {
        _logger = logger;
    }

    public async Task<List<PaytypeGlMappingDto>> GetAllMappingsAsync()
    {
        const string sql = @"
            SELECT
                glm.MappingID, glm.PayTypeID,
                ISNULL(p.PayTypeName, glm.PayTypeName) AS PayTypeName,
                glm.SapPayCategory, glm.SapGlAccount, glm.SapPayTypeName,
                CAST(glm.IsActive AS BIT) AS IsActive,
                glm.SortOrder, glm.Remarks, glm.UpdatedAt
            FROM paytype_gl_mapping glm
            INNER JOIN paytype p ON p.PayTypeID = glm.PayTypeID AND ISNULL(p.Deleted, 0) = 0
            ORDER BY glm.SortOrder, glm.PayTypeID";

        await using var conn = new SqlConnection(gbVar.MainConstr);
        var rows = await conn.QueryAsync<PaytypeGlMappingDto>(sql);
        return rows.ToList();
    }

    public async Task<List<UnmappedPaytypeDto>> GetUnmappedPaytypesAsync()
    {
        const string sql = @"
            SELECT p.PayTypeID, p.PayTypeName
            FROM paytype p
            WHERE ISNULL(p.Deleted, 0) = 0
              AND NOT EXISTS (
                SELECT 1 FROM paytype_gl_mapping glm WHERE glm.PayTypeID = p.PayTypeID
            )
            ORDER BY p.PayTypeID";

        await using var conn = new SqlConnection(gbVar.MainConstr);
        var rows = await conn.QueryAsync<UnmappedPaytypeDto>(sql);
        return rows.ToList();
    }

    public async Task<bool> UpsertMappingAsync(UpsertGlMappingDto dto)
    {
        const string sql = @"
            IF EXISTS (SELECT 1 FROM paytype_gl_mapping WHERE PayTypeID = @PayTypeID)
            BEGIN
                UPDATE paytype_gl_mapping SET
                    PayTypeName     = @PayTypeName,
                    SapPayCategory  = @SapPayCategory,
                    SapGlAccount    = @SapGlAccount,
                    SapPayTypeName  = @SapPayTypeName,
                    IsActive        = @IsActive,
                    SortOrder       = @SortOrder,
                    Remarks         = @Remarks,
                    UpdatedAt       = GETDATE()
                WHERE PayTypeID = @PayTypeID
            END
            ELSE
            BEGIN
                INSERT INTO paytype_gl_mapping
                    (PayTypeID, PayTypeName, SapPayCategory, SapGlAccount, SapPayTypeName, IsActive, SortOrder, Remarks)
                VALUES
                    (@PayTypeID, @PayTypeName, @SapPayCategory, @SapGlAccount, @SapPayTypeName, @IsActive, @SortOrder, @Remarks)
            END";

        await using var conn = new SqlConnection(gbVar.MainConstr);
        var affected = await conn.ExecuteAsync(sql, new
        {
            dto.PayTypeID,
            dto.PayTypeName,
            dto.SapPayCategory,
            dto.SapGlAccount,
            dto.SapPayTypeName,
            IsActive = dto.IsActive ? 1 : 0,
            dto.SortOrder,
            dto.Remarks
        });
        return affected > 0;
    }

    public async Task<bool> DeleteMappingAsync(int payTypeId)
    {
        const string sql = "DELETE FROM paytype_gl_mapping WHERE PayTypeID = @PayTypeID";
        await using var conn = new SqlConnection(gbVar.MainConstr);
        var affected = await conn.ExecuteAsync(sql, new { PayTypeID = payTypeId });
        return affected > 0;
    }
}
