using Dapper;
using Microsoft.Data.SqlClient;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Config;
using POS2SAP.API.Services.Interfaces;

namespace POS2SAP.API.Services.Implementations;

public class DeliveryDocTypeService : IDeliveryDocTypeService
{
    private static bool _schemaReady;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);

    private readonly ILogger<DeliveryDocTypeService> _logger;

    public DeliveryDocTypeService(ILogger<DeliveryDocTypeService> logger)
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
                END";

            const string seedSql = @"
                INSERT INTO dl_documenttype_mapping
                    (DocumentTypeID, DocumentTypeCode, DocumentTypeName, IsEnabled, SortOrder, Remarks)
                SELECT
                    dt.DocumentTypeID,
                    dt.DocumentTypeHeader,
                    ISNULL(NULLIF(LTRIM(RTRIM(dt.DocumentTypeName)), ''), dt.DocumentTypeHeader),
                    1,
                    dt.DocumentTypeID,
                    N'Default seed — Delivery stock-out type'
                FROM documenttype dt
                WHERE ISNULL(dt.Deleted, 0) = 0
                  AND dt.MovementInStock = -1
                  AND dt.DocumentTypeHeader IN ('STOCK-001','STOCK-002','STOCK-003','STOCK-004','STOCK-005')
                  AND NOT EXISTS (
                    SELECT 1 FROM dl_documenttype_mapping m WHERE m.DocumentTypeID = dt.DocumentTypeID
                )";

            await using var conn = new SqlConnection(gbVar.MainConstr);
            await conn.ExecuteAsync(createSql);
            var seeded = await conn.ExecuteAsync(seedSql);
            if (seeded > 0)
                _logger.LogInformation("Seeded {Count} default Delivery document types", seeded);

            _schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    public async Task<List<DeliveryDocTypeDto>> GetDocumentTypesAsync()
    {
        await EnsureSchemaAsync();

        const string sql = @"
            SELECT
                dt.DocumentTypeID                                           AS DocumentTypeId,
                dt.DocumentTypeHeader                                       AS DocumentTypeCode,
                ISNULL(NULLIF(LTRIM(RTRIM(dt.DocumentTypeName)), ''),
                       dt.DocumentTypeHeader)                               AS DocumentTypeName,
                CAST(CASE WHEN m.DocumentTypeID IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsEnabled
            FROM documenttype dt
            LEFT JOIN dl_documenttype_mapping m
                ON m.DocumentTypeID = dt.DocumentTypeID AND m.IsEnabled = 1
            WHERE ISNULL(dt.Deleted, 0) = 0
              AND dt.MovementInStock = -1
            ORDER BY dt.DocumentTypeHeader";

        await using var conn = new SqlConnection(gbVar.MainConstr);
        var rows = await conn.QueryAsync<DeliveryDocTypeDto>(sql);
        return rows.ToList();
    }

    public async Task<List<string>> GetEnabledTypeHeadersAsync()
    {
        await EnsureSchemaAsync();

        const string sql = @"
            SELECT m.DocumentTypeCode
            FROM dl_documenttype_mapping m
            WHERE m.IsEnabled = 1
            ORDER BY m.SortOrder, m.DocumentTypeCode";

        await using var conn = new SqlConnection(gbVar.MainConstr);
        var rows = await conn.QueryAsync<string>(sql);
        return rows.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<bool> SaveEnabledDocumentTypesAsync(IEnumerable<int> documentTypeIds)
    {
        await EnsureSchemaAsync();

        var ids = documentTypeIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? new List<int>();

        await using var conn = new SqlConnection(gbVar.MainConstr);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            await conn.ExecuteAsync(
                "UPDATE dl_documenttype_mapping SET IsEnabled = 0, UpdatedAt = GETDATE()",
                transaction: tx);

            if (ids.Count == 0)
            {
                await tx.CommitAsync();
                return true;
            }

            const string upsertSql = @"
                IF EXISTS (SELECT 1 FROM dl_documenttype_mapping WHERE DocumentTypeID = @DocumentTypeId)
                BEGIN
                    UPDATE dl_documenttype_mapping SET
                        DocumentTypeCode   = @DocumentTypeCode,
                        DocumentTypeName   = @DocumentTypeName,
                        IsEnabled          = 1,
                        SortOrder          = @SortOrder,
                        UpdatedAt          = GETDATE()
                    WHERE DocumentTypeID = @DocumentTypeId
                END
                ELSE
                BEGIN
                    INSERT INTO dl_documenttype_mapping
                        (DocumentTypeID, DocumentTypeCode, DocumentTypeName, IsEnabled, SortOrder, Remarks)
                    VALUES
                        (@DocumentTypeId, @DocumentTypeCode, @DocumentTypeName, 1, @SortOrder,
                         N'Selected from Delivery Document Type UI')
                END";

            foreach (var id in ids)
            {
                var row = await conn.QuerySingleOrDefaultAsync<(string Code, string Name)>(@"
                    SELECT
                        dt.DocumentTypeHeader AS Code,
                        ISNULL(NULLIF(LTRIM(RTRIM(dt.DocumentTypeName)), ''), dt.DocumentTypeHeader) AS Name
                    FROM documenttype dt
                    WHERE dt.DocumentTypeID = @Id
                      AND ISNULL(dt.Deleted, 0) = 0
                      AND dt.MovementInStock = -1",
                    new { Id = id }, tx);

                if (string.IsNullOrWhiteSpace(row.Code))
                {
                    _logger.LogWarning("Skip unknown document type id={Id} for Delivery mapping", id);
                    continue;
                }

                await conn.ExecuteAsync(upsertSql, new
                {
                    DocumentTypeId = id,
                    DocumentTypeCode = row.Code.Trim(),
                    DocumentTypeName = row.Name?.Trim() ?? row.Code.Trim(),
                    SortOrder = id
                }, tx);
            }

            await tx.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Failed to save Delivery document type mapping");
            throw;
        }
    }
}
