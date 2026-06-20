using POS2SAP.API.DTOs.Sap;

namespace POS2SAP.API.Services.Interfaces;

public interface IPosDataService
{
    // ------------------------------------------------------------------ AR Invoice

    /// <summary>ดึง POS bills ที่ยังไม่ได้ส่ง SAP (ไม่มีใน interface_logs หรือ status=RETRY)</summary>
    Task<List<SapArInvoiceHeadDto>> GetPendingBillsAsync(int batchSize = 500);

    /// <summary>ดึง POS bill เฉพาะ doc numbers ที่ระบุ</summary>
    Task<List<SapArInvoiceHeadDto>> GetBillsByDocNosAsync(IEnumerable<string> docNos);

    /// <summary>ดึง POS bills ตามช่วงวันที่ สาขา (optional) สำหรับหน้า Import Preview</summary>
    Task<List<SapArInvoiceHeadDto>> GetBillsByFilterAsync(DateTime dateFrom, DateTime dateTo, string? branchCode, int batchSize = 500);

    // ------------------------------------------------------------------ Incoming Payment

    /// <summary>
    /// ดึง POS receipts ที่ AR Invoice ส่ง SAP สำเร็จแล้ว (interface_logs status=SUCCESS, type=AR)
    /// แต่ยังไม่มี Incoming Payment (type=AP) ใน interface_logs
    /// </summary>
    Task<List<SapIncomingPaymentDto>> GetPendingPaymentsAsync(int batchSize = 500);

    /// <summary>ดึง payments เฉพาะ doc numbers ที่ระบุ (ไม่บังคับ AR SUCCESS dependency)</summary>
    Task<List<SapIncomingPaymentDto>> GetPaymentsByDocNosAsync(IEnumerable<string> docNos);

    /// <summary>ดึง payments ตามช่วงวันที่ สาขา สำหรับ Import Preview</summary>
    Task<List<SapIncomingPaymentDto>> GetPaymentsByFilterAsync(DateTime dateFrom, DateTime dateTo, string? branchCode, int batchSize = 500);
}
