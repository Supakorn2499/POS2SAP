using POS2SAP.API.DTOs.Sap;

namespace POS2SAP.API.Services.Interfaces;

public interface IPosDataService
{
    /// <summary>ดึง POS bills ที่ยังไม่ได้ส่ง SAP (ไม่มีใน interface_logs หรือ status=RETRY)</summary>
    Task<List<SapArInvoiceRequestDto>> GetPendingBillsAsync();

    /// <summary>ดึง POS bill เฉพาะ doc numbers ที่ระบุ</summary>
    Task<List<SapArInvoiceRequestDto>> GetBillsByDocNosAsync(IEnumerable<string> docNos);
}
