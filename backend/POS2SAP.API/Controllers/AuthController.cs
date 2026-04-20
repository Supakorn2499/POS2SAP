using System.Data;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Auth;

namespace POS2SAP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IDbConnection _dbConnection;

    public AuthController(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResultDto>>> Login([FromBody] LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.StaffLogin) || string.IsNullOrWhiteSpace(request.StaffPassword))
        {
            return BadRequest(ApiResponse<LoginResultDto>.Fail("กรุณากรอก username และ password"));
        }

        var hashedPassword = HashPassword(request.StaffPassword);

        var sql = @"
            SELECT StaffLogin, StaffFirstName, StaffLastName
            FROM staffs
            WHERE StaffLogin = @StaffLogin
              AND StaffPassword = @StaffPassword
              AND Deleted = 0
              AND Activated = 1
        ";

        var user = await _dbConnection.QueryFirstOrDefaultAsync<LoginResultDto>(
            sql,
            new { request.StaffLogin, StaffPassword = hashedPassword });

        if (user is null)
        {
            return Unauthorized(ApiResponse<LoginResultDto>.Fail("Username หรือ password ไม่ถูกต้อง", statusCode: 401));
        }

        return Ok(ApiResponse<LoginResultDto>.Ok(user, "เข้าสู่ระบบสำเร็จ"));
    }

    private static string HashPassword(string password)
    {
        using var sha1 = SHA1.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hashBytes = sha1.ComputeHash(bytes);
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty);
    }
}
