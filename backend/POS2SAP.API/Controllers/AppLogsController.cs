using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Logs;

namespace POS2SAP.API.Controllers;

/// <summary>Read Serilog application log files under Logs/.</summary>
[ApiController]
[Route("api/app-logs")]
[Authorize]
public class AppLogsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AppLogsController> _logger;

    public AppLogsController(IWebHostEnvironment env, ILogger<AppLogsController> logger)
    {
        _env = env;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<ApiResponse<List<AppLogFileDto>>> List()
    {
        var dir = GetLogsDirectory();
        if (!Directory.Exists(dir))
            return Ok(ApiResponse<List<AppLogFileDto>>.Ok(new List<AppLogFileDto>()));

        var files = Directory.GetFiles(dir, "pos2sap-*.log")
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new AppLogFileDto
            {
                FileName = f.Name,
                SizeBytes = f.Length,
                LastWriteUtc = f.LastWriteTimeUtc
            })
            .ToList();

        return Ok(ApiResponse<List<AppLogFileDto>>.Ok(files));
    }

    /// <summary>Return the last N lines of a log file (default 500, max 5000).</summary>
    [HttpGet("{fileName}")]
    public ActionResult<ApiResponse<AppLogContentDto>> GetTail(
        string fileName,
        [FromQuery] int lines = 500,
        [FromQuery] string? search = null)
    {
        if (lines < 1) lines = 500;
        if (lines > 5000) lines = 5000;

        var path = ResolveSafeLogPath(fileName);
        if (path is null)
            return BadRequest(ApiResponse<AppLogContentDto>.Fail("Invalid log file name"));

        if (!System.IO.File.Exists(path))
            return NotFound(ApiResponse<AppLogContentDto>.NotFound($"ไม่พบไฟล์ {fileName}"));

        try
        {
            // Share ReadWrite so Serilog can keep writing while we read
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            var all = new List<string>();
            while (!reader.EndOfStream)
                all.Add(reader.ReadLine() ?? string.Empty);

            IEnumerable<string> filtered = all;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim();
                filtered = all.Where(l => l.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            var matched = filtered.ToList();
            var tail = matched.Count <= lines
                ? matched
                : matched.Skip(matched.Count - lines).ToList();

            return Ok(ApiResponse<AppLogContentDto>.Ok(new AppLogContentDto
            {
                FileName = Path.GetFileName(path),
                Content = string.Join('\n', tail),
                LinesReturned = tail.Count,
                TotalLines = matched.Count
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed reading log file {File}", fileName);
            return StatusCode(500, ApiResponse<AppLogContentDto>.Fail("อ่านไฟล์ log ไม่สำเร็จ"));
        }
    }

    /// <summary>Clear (truncate/delete) one Serilog file.</summary>
    [HttpDelete("{fileName}")]
    public ActionResult<ApiResponse<object>> ClearOne(string fileName)
    {
        var path = ResolveSafeLogPath(fileName);
        if (path is null)
            return BadRequest(ApiResponse<object>.Fail("Invalid log file name"));

        if (!System.IO.File.Exists(path))
            return NotFound(ApiResponse<object>.NotFound($"ไม่พบไฟล์ {fileName}"));

        try
        {
            ClearLogFile(path);
            _logger.LogInformation("App log cleared: {File}", fileName);
            return Ok(ApiResponse<object>.Ok(new { cleared = 1, fileName }, "ล้าง log สำเร็จ"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed clearing log file {File}", fileName);
            return StatusCode(500, ApiResponse<object>.Fail("ล้างไฟล์ log ไม่สำเร็จ"));
        }
    }

    /// <summary>Clear all pos2sap-*.log files under Logs/.</summary>
    [HttpDelete]
    public ActionResult<ApiResponse<object>> ClearAll()
    {
        var dir = GetLogsDirectory();
        if (!Directory.Exists(dir))
            return Ok(ApiResponse<object>.Ok(new { cleared = 0 }, "ไม่มีไฟล์ log"));

        var cleared = 0;
        var failed = new List<string>();
        foreach (var path in Directory.GetFiles(dir, "pos2sap-*.log"))
        {
            try
            {
                ClearLogFile(path);
                cleared++;
            }
            catch (Exception ex)
            {
                failed.Add(Path.GetFileName(path));
                _logger.LogWarning(ex, "Failed clearing log file {File}", path);
            }
        }

        if (failed.Count > 0 && cleared == 0)
            return StatusCode(500, ApiResponse<object>.Fail($"ล้าง log ไม่สำเร็จ: {string.Join(", ", failed)}"));

        _logger.LogInformation("App logs cleared: {Cleared} file(s)", cleared);
        return Ok(ApiResponse<object>.Ok(
            new { cleared, failed },
            failed.Count == 0 ? "ล้าง log ทั้งหมดสำเร็จ" : $"ล้างได้ {cleared} ไฟล์ (ล้มเหลว {failed.Count})"));
    }

    // ponytail: truncate when Serilog still holds the file; delete older rolled files
    private static void ClearLogFile(string path)
    {
        try
        {
            System.IO.File.Delete(path);
            return;
        }
        catch (IOException)
        {
            // in use — truncate instead
        }

        using var fs = new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite);
    }

    private string GetLogsDirectory() =>
        Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Logs"));

    private string? ResolveSafeLogPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            return null;
        if (!fileName.StartsWith("pos2sap-", StringComparison.OrdinalIgnoreCase)
            || !fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            return null;

        var full = Path.GetFullPath(Path.Combine(GetLogsDirectory(), fileName));
        var root = GetLogsDirectory();
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return null;
        return full;
    }
}
