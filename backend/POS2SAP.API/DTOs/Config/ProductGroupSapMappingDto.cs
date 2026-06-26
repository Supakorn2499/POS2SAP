namespace POS2SAP.API.DTOs.Config;

public class ProductGroupSapMappingDto
{
    public int    MappingID          { get; set; }
    public int    ProductGroupID     { get; set; }
    public string ProductGroupCode   { get; set; } = string.Empty;
    public string ProductGroupName   { get; set; } = string.Empty;
    public string? SapItemGroupCode  { get; set; }
    public string? SapItemGroupName  { get; set; }
    public bool   IsActive           { get; set; } = true;
    public int    SortOrder          { get; set; }
    public string? Remarks           { get; set; }
    public DateTime UpdatedAt        { get; set; }
}

public class UnmappedProductGroupDto
{
    public int    ProductGroupID   { get; set; }
    public string ProductGroupCode { get; set; } = string.Empty;
    public string ProductGroupName { get; set; } = string.Empty;
}

public class UpsertProductGroupMappingDto
{
    public int    ProductGroupID     { get; set; }
    public string ProductGroupCode   { get; set; } = string.Empty;
    public string ProductGroupName   { get; set; } = string.Empty;
    public string? SapItemGroupCode  { get; set; }
    public string? SapItemGroupName  { get; set; }
    public bool   IsActive           { get; set; } = true;
    public int    SortOrder          { get; set; }
    public string? Remarks           { get; set; }
}
