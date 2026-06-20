namespace POS2SAP.API.DTOs.Config;

public class PaytypeGlMappingDto
{
    public int    MappingID      { get; set; }
    public int    PayTypeID      { get; set; }
    public string PayTypeName    { get; set; } = string.Empty;
    public string SapPayCategory { get; set; } = "SKIP";
    public string? SapGlAccount  { get; set; }
    public string? SapPayTypeName{ get; set; }
    public bool   IsActive       { get; set; } = true;
    public int    SortOrder      { get; set; }
    public string? Remarks       { get; set; }
    public DateTime UpdatedAt    { get; set; }
}

public class UnmappedPaytypeDto
{
    public int    PayTypeID   { get; set; }
    public string PayTypeName { get; set; } = string.Empty;
}

public class UpsertGlMappingDto
{
    public int    PayTypeID      { get; set; }
    public string PayTypeName    { get; set; } = string.Empty;
    public string SapPayCategory { get; set; } = "SKIP";
    public string? SapGlAccount  { get; set; }
    public string? SapPayTypeName{ get; set; }
    public bool   IsActive       { get; set; } = true;
    public int    SortOrder      { get; set; }
    public string? Remarks       { get; set; }
}
