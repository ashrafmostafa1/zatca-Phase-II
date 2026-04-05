using System;
using System.Threading.Tasks;
using System.Xml;
using Zatca_Phase_II.Eum;
using Zatca_Phase_II.Helpers;
using Zatca_Phase_II.Interfaces;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Services;

public class ZatcaRoutingService
{
    private readonly IEInvoiceService _eInvoiceService;
    private readonly IOutboxEInvoiceService _outboxService;

    public ZatcaRoutingService(IEInvoiceService eInvoiceService, IOutboxEInvoiceService outboxService)
    {
        _eInvoiceService = eInvoiceService;
        _outboxService = outboxService;
    }

    /// <summary>
    /// Routes the invoice to either synchronous Clearance (B2B) or asynchronous Reporting (B2C) pipeline.
    /// </summary>
    public async Task<ZatcaResult> ProcessInvoiceAsync(Bill bill, ZatcaBranch branch, EnvironmentTyp env)
    {
        // 1. Validation
        var validationResult = ZatcaPreValidation.Validate(bill);
        if (!validationResult.IsSuccess)
        {
            return validationResult;
        }

        // 2. Identify B2B vs B2C based on VAT presence
        bool isB2B = !string.IsNullOrWhiteSpace(bill.Customer?.TaxNumber) && bill.Customer.TaxNumber.Length == 15;

        // 3. E-Invoice Generation (Signing + Hash + initial QR)
        var fatoora = await _eInvoiceService.EInvoice(bill, branch, env);
        if (fatoora == null || fatoora.ObjRequest == null)
        {
            return ZatcaResult.Failure("Failed to locally generate and sign the E-Invoice XML/Hash.");
        }

        // 4. Routing Action
        if (isB2B)
        {
            // --- B2B: Standard Tax Invoice ---
            // Must process Clearance synchronously and wait for cleared XML.
            try
            {
                var response = await _eInvoiceService.ClearanceEInvoice(fatoora.ObjRequest, branch, env);

                if (response != null && response.HttpStatus == System.Net.HttpStatusCode.OK)
                {
                    // The ClearedInvoice Base64 contains ZATCA's cryptographic stamp.
                    string? clearedXmlBase64 = response.ClearedInvoice;
                    string b2bQr = string.Empty;

                    if (!string.IsNullOrEmpty(clearedXmlBase64))
                    {
                        var xmlBytes = Convert.FromBase64String(clearedXmlBase64);
                        var xmlStr = System.Text.Encoding.UTF8.GetString(xmlBytes);
                        var xDoc = new XmlDocument();
                        xDoc.LoadXml(xmlStr);
                        
                        // Extract QR right from the ZATCA Cleared XML
                        b2bQr = CryptoUtility.ExtractQrFromSignedXml(xDoc) ?? string.Empty;
                    }

                    var res = ZatcaResult.Success(true, b2bQr, clearedXmlBase64);
                    res.ZATCAApiStatus = response;
                    return res;
                }
                else
                {
                    return ZatcaResult.Failure($"Clearance API rejected the invoice. Status: {response?.HttpStatus}");
                }
            }
            catch (Exception ex)
            {
                return ZatcaResult.Failure($"B2B Synchronous Clearance Failed: {ex.Message}");
            }
        }
        else
        {
            // --- B2C: Simplified Tax Invoice ---
            // Generate QR code locally, finalize immediately, and queue for Reporting API.
            try
            {
                // Dispatch exactly the same bill into outbox queue.
                // The fatoora's Base64QR is perfectly valid for local B2C receipt printing.
                await _outboxService.EnqueueAsync(bill, branch, env);

                var res = ZatcaResult.Success(false, fatoora.Base64QR);
                res.ZATCAApiStatus = "Enqueued for asynchronous Reporting";
                return res;
            }
            catch (Exception ex)
            {
                return ZatcaResult.Failure($"B2C Reporting Outbox Insertion Failed: {ex.Message}");
            }
        }
    }
}
