namespace POS2SAP.API.Services.Interfaces;

public interface IInterfaceJobService
{
    /// <summary>Manual trigger — ส่งทั้งหมด (PENDING/RETRY) หรือเฉพาะ docNos</summary>
    Task<(int Sent, int Failed)> TriggerManualAsync(IEnumerable<string>? docNos = null);

    /// <summary>Retry single FAILED record</summary>
    Task<bool> RetryAsync(string logId);

    /// <summary>Import preview — ดึงข้อมูลจาก POS แล้ว insert เป็น PENDING ยังไม่ส่ง SAP</summary>
    Task<(int Fetched, int Imported, string? Error)> ImportPreviewAsync(IEnumerable<string>? docNos = null);
}
