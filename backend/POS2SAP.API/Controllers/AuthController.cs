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
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IDbConnection dbConnection,
        IJwtTokenService jwtTokenService,
        JwtSettings jwtSettings,
        ILogger<AuthController> logger)
    {
        _dbConnection = dbConnection;
        _jwtTokenService = jwtTokenService;
        _jwtSettings = jwtSettings;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResultDto>>> Login([FromBody] LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.StaffLogin) || string.IsNullOrWhiteSpace(request.StaffPassword))
        {
            return BadRequest(ApiResponse<LoginResultDto>.Fail("กรุณากรอก username และ password"));
        }

        var sql = @"
            SELECT StaffLogin, StaffFirstName, StaffLastName, StaffPassword
            FROM staffs
            WHERE StaffLogin = @StaffLogin
              AND Deleted = 0
              AND Activated = 1
        ";

        var user = await _dbConnection.QueryFirstOrDefaultAsync<dynamic>(
            sql,
            new { request.StaffLogin });

        if (user is null)
        {
            _logger.LogWarning("Login attempt failed: username not found - {StaffLogin}", request.StaffLogin);
            return Unauthorized(ApiResponse<LoginResultDto>.Fail("Username หรือ password ไม่ถูกต้อง", statusCode: 401));
        }

        // Verify password — supports BCrypt (new) and SHA1 legacy hashes (auto-upgrade on success)
        var storedHash = (string?)user.StaffPassword;
        if (string.IsNullOrEmpty(storedHash))
        {
            _logger.LogWarning("Login attempt failed: no password hash - {StaffLogin}", request.StaffLogin);
            return Unauthorized(ApiResponse<LoginResultDto>.Fail("Username หรือ password ไม่ถูกต้อง", statusCode: 401));
        }

        bool passwordValid;
        bool isLegacyHash = !storedHash.StartsWith("$2", StringComparison.Ordinal);

        try
        {
            if (isLegacyHash)
            {
                passwordValid = VerifyLegacySha1(request.StaffPassword, storedHash);
            }
            else
            {
                passwordValid = BCrypt.Net.BCrypt.Verify(request.StaffPassword, storedHash);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Password verification error for user {StaffLogin}", request.StaffLogin);
            return Unauthorized(ApiResponse<LoginResultDto>.Fail("Username หรือ password ไม่ถูกต้อง", statusCode: 401));
        }

        if (!passwordValid)
        {
            _logger.LogWarning("Login attempt failed: invalid password - {StaffLogin}", request.StaffLogin);
            return Unauthorized(ApiResponse<LoginResultDto>.Fail("Username หรือ password ไม่ถูกต้อง", statusCode: 401));
        }

        // Upgrade legacy SHA1 hash → BCrypt on successful login
        if (isLegacyHash)
        {
            try
            {
                var newHash = BCrypt.Net.BCrypt.HashPassword(request.StaffPassword, workFactor: 11);
                await _dbConnection.ExecuteAsync(
                    "UPDATE staffs SET StaffPassword = @NewHash, UpdatedAt = GETDATE() WHERE StaffLogin = @StaffLogin",
                    new { NewHash = newHash, StaffLogin = (string)user.StaffLogin });
                _logger.LogInformation("Upgraded legacy password hash to BCrypt for {StaffLogin}", request.StaffLogin);
            }
            catch (Exception ex)
            {
                // Non-fatal: login still succeeds even if upgrade fails
                _logger.LogWarning(ex, "Failed to upgrade legacy hash for {StaffLogin}", request.StaffLogin);
            }
        }

        // Generate tokens
        var accessToken = _jwtTokenService.GenerateAccessToken(
            (string)user.StaffLogin,
            (string)user.StaffFirstName,
            (string)user.StaffLastName);

        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        // Save refresh token to database
        await SaveRefreshTokenAsync((string)user.StaffLogin, refreshToken);

        var result = new LoginResultDto
        {
            StaffLogin = (string)user.StaffLogin,
            StaffFirstName = (string)user.StaffFirstName,
            StaffLastName = (string)user.StaffLastName,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwtSettings.ExpirationMinutes * 60
        };

        _logger.LogInformation("Login successful: {StaffLogin}", request.StaffLogin);
        return Ok(ApiResponse<LoginResultDto>.Ok(result, "เข้าสู่ระบบสำเร็จ"));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<RefreshTokenResponseDto>>> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(ApiResponse<RefreshTokenResponseDto>.Fail("กรุณาระบุ refresh token"));
        }

        // Verify refresh token exists in database
        var staffLogin = await VerifyRefreshTokenAsync(request.RefreshToken);
        if (string.IsNullOrEmpty(staffLogin))
        {
            _logger.LogWarning("Invalid refresh token attempt");
            return Unauthorized(ApiResponse<RefreshTokenResponseDto>.Fail("Refresh token ไม่ถูกต้องหรือหมดอายุ", statusCode: 401));
        }

        // Get user info
        var sql = @"SELECT StaffFirstName, StaffLastName FROM staffs WHERE StaffLogin = @StaffLogin AND Deleted = 0";
        var user = await _dbConnection.QueryFirstOrDefaultAsync<dynamic>(sql, new { StaffLogin = staffLogin });

        if (user is null)
        {
            return Unauthorized(ApiResponse<RefreshTokenResponseDto>.Fail("ผู้ใช้ไม่พบ", statusCode: 401));
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(
            staffLogin,
            (string)user.StaffFirstName,
            (string)user.StaffLastName);

        var result = new RefreshTokenResponseDto
        {
            AccessToken = accessToken,
            ExpiresIn = _jwtSettings.ExpirationMinutes * 60
        };

        _logger.LogInformation("Token refreshed: {StaffLogin}", staffLogin);
        return Ok(ApiResponse<RefreshTokenResponseDto>.Ok(result, "Refresh token สำเร็จ"));
    }

    private async Task SaveRefreshTokenAsync(string staffLogin, string refreshToken)
    {
        try
        {
            var expiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
            var sql = @"
                IF NOT EXISTS (SELECT 1 FROM refresh_tokens WHERE staff_login = @StaffLogin)
                    INSERT INTO refresh_tokens (staff_login, token, expires_at, created_at)
                    VALUES (@StaffLogin, @Token, @ExpiresAt, GETUTCDATE())
                ELSE
                    UPDATE refresh_tokens 
                    SET token = @Token, expires_at = @ExpiresAt, updated_at = GETUTCDATE()
                    WHERE staff_login = @StaffLogin
            ";

            await _dbConnection.ExecuteAsync(sql, new
            {
                StaffLogin = staffLogin,
                Token = refreshToken,
                ExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving refresh token for {StaffLogin}", staffLogin);
        }
    }

    private async Task<string?> VerifyRefreshTokenAsync(string refreshToken)
    {
        try
        {
            var sql = @"
                SELECT staff_login FROM refresh_tokens 
                WHERE token = @Token AND expires_at > GETUTCDATE()
            ";

            var result = await _dbConnection.QueryFirstOrDefaultAsync<string>(
                sql,
                new { Token = refreshToken });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying refresh token");
            return null;
        }
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    }

    /// <summary>
    /// Verifies a password against a legacy SHA1 hash from the original POS system.
    /// Accepts hex (40 chars, upper or lower case) or Base64 encoded SHA1.
    /// </summary>
    private static bool VerifyLegacySha1(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;

        using var sha1 = SHA1.Create();
        var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(password));

        var hex = Convert.ToHexString(bytes); // upper-case hex
        if (string.Equals(hex, storedHash, StringComparison.OrdinalIgnoreCase))
            return true;

        var base64 = Convert.ToBase64String(bytes);
        if (string.Equals(base64, storedHash, StringComparison.Ordinal))
            return true;

        return false;
    }
}

