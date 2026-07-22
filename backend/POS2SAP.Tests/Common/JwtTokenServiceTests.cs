using Microsoft.Extensions.Logging.Abstractions;
using POS2SAP.API.Common;

namespace POS2SAP.Tests.Common;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(string? secret = null) =>
        new(
            new JwtSettings
            {
                Secret = secret ?? "dev-secret-key-minimum-32-characters-for-dev-only-CHANGE-IN-PRODUCTION",
                ExpirationMinutes = 60,
                Issuer = "POS2SAP",
                Audience = "POS2SAPUsers"
            },
            NullLogger<JwtTokenService>.Instance);

    [Fact]
    public void GenerateAndValidate_RoundTrip_Succeeds()
    {
        var svc = CreateService();
        var token = svc.GenerateAccessToken("vtec", "Test", "User");
        var principal = svc.ValidateToken(token);

        Assert.NotNull(principal);
        Assert.Equal("vtec", principal!.FindFirst("staffLogin")?.Value);
    }

    [Fact]
    public void ValidateToken_WrongSecret_ReturnsNull()
    {
        var token = CreateService().GenerateAccessToken("vtec", "A", "B");
        var other = CreateService("other-secret-key-minimum-32-characters-xxxxxxxx");
        Assert.Null(other.ValidateToken(token));
    }

    [Fact]
    public void ValidateToken_Expired_ReturnsNull()
    {
        var svc = new JwtTokenService(
            new JwtSettings
            {
                Secret = "dev-secret-key-minimum-32-characters-for-dev-only-CHANGE-IN-PRODUCTION",
                ExpirationMinutes = -1, // already expired
                Issuer = "POS2SAP",
                Audience = "POS2SAPUsers"
            },
            NullLogger<JwtTokenService>.Instance);

        var token = svc.GenerateAccessToken("vtec", "A", "B");
        Assert.Null(svc.ValidateToken(token));
    }

    [Fact]
    public void GenerateRefreshToken_IsUniqueBase64()
    {
        var svc = CreateService();
        var a = svc.GenerateRefreshToken();
        var b = svc.GenerateRefreshToken();
        Assert.NotEqual(a, b);
        Assert.False(string.IsNullOrWhiteSpace(a));
        // base64 of 32 bytes
        Assert.True(Convert.FromBase64String(a).Length >= 16);
    }
}
