namespace Zatca_Phase_II.Models;

public class TestUploadResponse
{
    public ValidationResults ValidationResults { get; set; } = new ValidationResults();
    public string ReportingStatus { get; set; } = string.Empty;
    public object ClearanceStatus { get; set; } = new object();
    public object QrSellerStatus { get; set; } = new object();
    public object QrBuyerStatus { get; set; } = new object();
}
