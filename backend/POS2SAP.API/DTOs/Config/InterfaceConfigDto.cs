namespace POS2SAP.API.DTOs.Config;

public class InterfaceConfigDto
{
    public string Id { get; set; } = string.Empty;
    public string ConfigKey { get; set; } = string.Empty;
    public string? ConfigValue { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateConfigDto
{
    public string ConfigValue { get; set; } = string.Empty;
}
