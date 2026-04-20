namespace POS2SAP.API.DTOs.Auth;

public class LoginRequestDto
{
    public string StaffLogin { get; set; } = string.Empty;
    public string StaffPassword { get; set; } = string.Empty;
}
