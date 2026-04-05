using System.Net;

namespace Zatca_Phase_II.Models;

public class TestUploadResponse
{
    public ValidationResults ValidationResults { get; set; } = new ValidationResults();
    public string ReportingStatus  { get; set; } = string.Empty;
    public object ClearanceStatus  { get; set; } = new object();
    public object QrSellerStatus   { get; set; } = new object();
    public object QrBuyerStatus    { get; set; } = new object();
    public string? ClearedInvoice  { get; set; }

    // ── Duplicate / error enrichment ──────────────────────────────────────
    /// <summary>HTTP status code returned by ZATCA.</summary>
    public HttpStatusCode HttpStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>
    /// True when ZATCA rejected this submission because the same invoice
    /// UUID was already reported/cleared (error code IVS-DUP-001).
    /// </summary>
    public bool IsDuplicate { get; set; } = false;

    /// <summary>Raw JSON body from ZATCA (useful for logging/debugging).</summary>
    public string RawResponse { get; set; } = string.Empty;
}
