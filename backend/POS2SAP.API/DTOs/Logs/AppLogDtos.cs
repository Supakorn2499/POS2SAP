namespace POS2SAP.API.DTOs.Logs;

public class AppLogFileDto
{
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastWriteUtc { get; set; }
}

public class AppLogContentDto
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int LinesReturned { get; set; }
    public int TotalLines { get; set; }
}
