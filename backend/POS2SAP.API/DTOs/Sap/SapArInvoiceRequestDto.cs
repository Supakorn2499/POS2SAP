namespace POS2SAP.API.DTOs.Sap;

public class SapArInvoiceRequestDto
{
    public SapArInvoiceHeadDto Head { get; set; } = new();
    public List<SapArInvoiceLineDto> Lines { get; set; } = new();
}
