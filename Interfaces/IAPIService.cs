using Zatca_Phase_II.Eum;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Interfaces;

/// <summary>
/// Abstraction over the raw ZATCA HTTP API.
/// Inject this interface instead of the concrete <c>APIService</c>
/// so each tier (XML, signing, API) can be tested or swapped independently.
/// </summary>
public interface IAPIService
{
    // ── Onboarding ────────────────────────────────────────────────────────────

    /// <summary>API #3 — Request a Compliance CSID.</summary>
    Task<ComplianceResponse?> SubmitComplianceRequestAsync(string csr, string otp);

    /// <summary>API #4 — Onboard a Production CSID using a compliance request ID.</summary>
    Task<ComplianceResponse?> SubmitProductionRequestAsync(
        string compliance_request_id,
        string username,
        string password
    );

    /// <summary>API #5 — Renew an existing Production CSID (PATCH).</summary>
    Task<ComplianceResponse?> RenewProductionCsidAsync(
        string csr,
        string otp,
        string username,
        string password
    );

    // ── Compliance check ─────────────────────────────────────────────────────

    /// <summary>API #6 — Compliance invoice check (simulation/onboarding only).</summary>
    Task<TestUploadResponse?> UploadInvTest(object request, string username, string password);

    // ── Operational ───────────────────────────────────────────────────────────

    /// <summary>
    /// API #2 — Clearance (Standard / B2B invoices). Sends the invoice for ZATCA
    /// to stamp and return a cleared copy.
    /// </summary>
    Task<TestUploadResponse?> UploadClearance(object request, string username, string password);

    /// <summary>
    /// API #1 — Reporting (Simplified / B2C invoices). Reports the invoice to
    /// ZATCA without requiring a cleared copy back.
    /// </summary>
    Task<TestUploadResponse?> UploadCore(object request, string username, string password);
}
