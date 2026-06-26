-- =============================================================================
-- POS2SAP — optional performance indexes (run on HQ_FAMTIME after DBA review)
-- Idempotent: skips indexes that already exist.
-- =============================================================================
USE HQ_FAMTIME;
GO

-- interface_logs — scheduler send queue + duplicate checks
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_interface_logs_type_status_doc' AND object_id = OBJECT_ID('interface_logs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_interface_logs_type_status_doc
        ON interface_logs (interface_type, status, is_deleted)
        INCLUDE (pos_doc_no, pos_doc_date, branch_code, retry_count, created_at);
    PRINT 'Created IX_interface_logs_type_status_doc';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_interface_logs_pos_doc_type' AND object_id = OBJECT_ID('interface_logs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_interface_logs_pos_doc_type
        ON interface_logs (pos_doc_no, interface_type, is_deleted)
        INCLUDE (status);
    PRINT 'Created IX_interface_logs_pos_doc_type';
END
GO

-- AR / AP — ordertransaction date-range scans
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ordertransaction_sale_status' AND object_id = OBJECT_ID('ordertransaction'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ordertransaction_sale_status
        ON ordertransaction (SaleDate, TransactionStatusID)
        INCLUDE (ReceiptNumber, TranKey, Deleted)
        WHERE ISNULL(Deleted, 0) = 0;
    PRINT 'Created IX_ordertransaction_sale_status';
END
GO

-- Delivery — document date-range scans
IF OBJECT_ID('document', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_document_date_no' AND object_id = OBJECT_ID('document'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_document_date_no
        ON document (DocumentDate)
        INCLUDE (DocumentNo, DocumentKey, DocumentTypeID);
    PRINT 'Created IX_document_date_no';
END
GO

PRINT 'perf_indexes.sql complete';
