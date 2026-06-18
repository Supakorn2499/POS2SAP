using System.ComponentModel.DataAnnotations;
using POS2SAP.API.DTOs.Sap;

namespace POS2SAP.API.DTOs.Monitor;

/// <summary>
/// DTO for receiving resend requests from the frontend.
/// Wraps a list of SapArInvoiceHeadDto in a 'Request' property.
/// </summary>
public class ResendRequestDto
{
    [Required(ErrorMessage = "The request field is required.")]
    public List<SapArInvoiceHeadDto> Request { get; set; } = new List<SapArInvoiceHeadDto>();
}