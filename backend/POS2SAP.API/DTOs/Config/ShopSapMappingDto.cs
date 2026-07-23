namespace POS2SAP.API.DTOs.Config;

public class ShopSapMappingDto
{
    public int    MappingID     { get; set; }
    public int    ShopID        { get; set; }
    public string ShopCode      { get; set; } = string.Empty;
    public string ShopName      { get; set; } = string.Empty;
    /// <summary>POS shop_data.SLOC (read-only reference)</summary>
    public string PosSloc       { get; set; } = string.Empty;
    /// <summary>POS PTTShopCode / shopcode (read-only reference)</summary>
    public string PosBranchCode { get; set; } = string.Empty;
    /// <summary>POS BranchNo (read-only reference)</summary>
    public string PosVatBranch  { get; set; } = string.Empty;
    public string? SapCardCode   { get; set; }
    public string? SapBranchCode { get; set; }
    public string? SapBranchName { get; set; }
    public string? SapVatBranch  { get; set; }
    public bool   IsActive      { get; set; } = true;
    public int    SortOrder     { get; set; }
    public string? Remarks      { get; set; }
    public DateTime UpdatedAt   { get; set; }
}

public class UnmappedShopDto
{
    public int    ShopID   { get; set; }
    public string ShopCode { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
}

public class UpsertShopMappingDto
{
    public int    ShopID         { get; set; }
    public string ShopCode       { get; set; } = string.Empty;
    public string ShopName       { get; set; } = string.Empty;
    public string? SapCardCode   { get; set; }
    public string? SapBranchCode { get; set; }
    public string? SapBranchName { get; set; }
    public string? SapVatBranch  { get; set; }
    public bool   IsActive       { get; set; } = true;
    public int    SortOrder      { get; set; }
    public string? Remarks       { get; set; }
}
