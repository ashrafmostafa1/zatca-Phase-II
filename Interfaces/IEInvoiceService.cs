using Zatca_Phase_II.Eum;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Interfaces;

public interface IEInvoiceService
{
    /// <summary>
    /// Generates the UBL 2.1 XML, signs it, and returns the signed payload + metadata.
    /// Does NOT submit to ZATCA — call <see cref="UploadEInvoice"/> or
    /// <see cref="ClearanceEInvoice"/> after this.
    /// </summary>
    Task<Fatoora?> EInvoice(Bill obj, ZatcaBranch zatcaBranch, EnvironmentTyp type);

    /// <summary>
    /// API #1 — Reporting endpoint for Simplified (B2C) invoices.
    /// </summary>
    Task<TestUploadResponse?> UploadEInvoice(
        object objRequest,
        ZatcaBranch zatcaBranch,
        EnvironmentTyp type
    );

    /// <summary>
    /// API #2 — Clearance endpoint for Standard (B2B) invoices.
    /// Returns a stamped invoice from ZATCA.
    /// </summary>
    Task<TestUploadResponse?> ClearanceEInvoice(
        object objRequest,
        ZatcaBranch zatcaBranch,
        EnvironmentTyp type
    );
}

