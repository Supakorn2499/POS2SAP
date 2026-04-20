namespace POS2SAP.API.DTOs.Monitor;

/// <summary>Full detail — includes JSON payloads</summary>
public class InterfaceLogDetailDto : InterfaceLogDto
{
    public string? PosData { get; set; }       // JSON snapshot POS source
    public string? SapRequest { get; set; }    // JSON sent to SAP
    public string? SapResponse { get; set; }   // JSON from SAP
}
