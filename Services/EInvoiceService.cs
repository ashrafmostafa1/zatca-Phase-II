using Microsoft.Extensions.Hosting;
using Zatca_Phase_II.Eum;
using Zatca_Phase_II.Helpers;
using Zatca_Phase_II.Interfaces;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Services;

/// <summary>
/// High-level e-invoice service.
///
/// Clean Architecture role: this is the Application Layer — it orchestrates
/// the domain services (XML generation, signing) and the infrastructure
/// service (<see cref="IAPIService"/>) without knowing about HTTP details.
///
/// Routing rule:
///   Standard  (B2B) invoices → Clearance API (#2)
///   Simplified (B2C) invoices → Reporting API (#1)
/// </summary>
public class EInvoiceService : IEInvoiceService
{
    private readonly IHostEnvironment _hostingEnvironment;
    private readonly IAPIService _apiService;

    /// <summary>
    /// Primary constructor — use this when DI is available.
    /// </summary>
    public EInvoiceService(IHostEnvironment hostingEnvironment, IAPIService apiService)
    {
        _hostingEnvironment = hostingEnvironment;
        _apiService         = apiService;
    }

    /// <summary>
    /// Legacy constructor for callers that don't use DI yet.
    /// Creates an <see cref="APIService"/> with the given simulation flag.
    /// </summary>
    public EInvoiceService(IHostEnvironment hostingEnvironment, bool isSimulation = false)
        : this(hostingEnvironment, new APIService(new ZatcaOptions { IsSimulation = isSimulation }))
    { }

    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Fatoora?> EInvoice(Bill obj, ZatcaBranch zatcaBranch, EnvironmentTyp type)
    {
        try
        {
            var zatca       = new ZatcaXML(_hostingEnvironment, type);
            var signsService = new SignsService(_hostingEnvironment, type == EnvironmentTyp.Simulation);

            var xml = zatca.CreateXML(obj);
            var res = await signsService.SignAndSync(
                xml,
                zatcaBranch.Csr,
                zatcaBranch.PrivateKey,
                Guid.Parse(obj.Uid),
                zatcaBranch.BinarySecurityToken,
                zatcaBranch.SecretKey
            );

            return new Fatoora
            {
                BillId     = obj.Id,
                HashXML    = res.Item1!,
                Base64QR   = res.Item2!,
                Path       = res.Item3!,
                ObjRequest = res.Item4!,
            };
        }
        catch (Exception ex)
        {
            LogsFile.MessageZatca($"Error in EInvoice: {ex.Message}");
            return null;
        }
    }

    // ── Reporting — Simplified (B2C) ──────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<TestUploadResponse?> UploadEInvoice(
        object objRequest,
        ZatcaBranch zatcaBranch,
        EnvironmentTyp type
    )
    {
        LogsFile.MessageZatca("[EInvoiceService] Reporting (Simplified/B2C)");
        return await _apiService.UploadCore(
            objRequest,
            zatcaBranch.BinarySecurityToken,
            zatcaBranch.SecretKey
        );
    }

    // ── Clearance — Standard (B2B) ────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<TestUploadResponse?> ClearanceEInvoice(
        object objRequest,
        ZatcaBranch zatcaBranch,
        EnvironmentTyp type
    )
    {
        LogsFile.MessageZatca("[EInvoiceService] Clearance (Standard/B2B)");
        return await _apiService.UploadClearance(
            objRequest,
            zatcaBranch.BinarySecurityToken,
            zatcaBranch.SecretKey
        );
    }

    // ── Convenience method — auto-routes based on invoice type ────────────────

    /// <summary>
    /// Builds the signed invoice and submits it to the correct ZATCA endpoint
    /// based on the bill's <see cref="InvoiceType"/> (Standard → Clearance, Simplified → Reporting).
    /// This is the single entry point the POS should call after saving the bill.
    /// </summary>
    public async Task<TestUploadResponse?> EInvoiceAndSubmitAsync(
        Bill bill,
        ZatcaBranch zatcaBranch,
        EnvironmentTyp type
    )
    {
        var fatoora = await EInvoice(bill, zatcaBranch, type);
        if (fatoora == null)
        {
            LogsFile.MessageZatca("[EInvoiceService] EInvoiceAndSubmit — signing failed, skipping API call");
            return null;
        }

        return bill.InvoiceType == InvoiceType.Standard
            ? await ClearanceEInvoice(fatoora.ObjRequest, zatcaBranch, type)
            : await UploadEInvoice(fatoora.ObjRequest, zatcaBranch, type);
    }
}
