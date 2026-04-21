namespace POS2SAP.API.DTOs.Auth;

public class LoginResultDto
{
    public string StaffLogin { get; set; } = string.Empty;
    public string StaffFirstName { get; set; } = string.Empty;
    public string StaffLastName { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }  // seconds
}

