namespace POS2SAP.API.Common;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 7;
    public string Issuer { get; set; } = "POS2SAP";
    public string Audience { get; set; } = "POS2SAPUsers";
}
