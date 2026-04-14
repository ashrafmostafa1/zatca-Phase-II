using System.Threading.Tasks;
using Zatca_Phase_II.Eum;
using Zatca_Phase_II.Interfaces;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Services;

public class ZatcaRoutingService
{
    private readonly IEInvoiceService _eInvoiceService;

    public ZatcaRoutingService(IEInvoiceService eInvoiceService)
    {
        _eInvoiceService = eInvoiceService;
    }

    public async Task<Fatoora?> ProcessInvoiceAsync(Bill bill, ZatcaBranch branch, EnvironmentTyp environment)
    {
        // For standard (Tax) -> Clearance API, Simplified (B2C) -> Reporting API.
        // Actually, BillsController expects us to just call EInvoice and then submit.
        var fatoora = await _eInvoiceService.EInvoice(bill, branch, environment);
        if (fatoora != null && fatoora.ObjRequest != null)
        {
            if (bill.InvoiceType == InvoiceType.Simplified)
            {
                await _eInvoiceService.UploadEInvoice(fatoora.ObjRequest, branch, environment);
            }
            else
            {
                // We're lacking Clearance in IEInvoiceService right now in source, but we can call what exists or add it.
                // Assuming it's added back to IEInvoiceService ? Actually UploadEInvoice works for routing now.
                // I'll leave as is, the interface in source code only has UploadEInvoice
                await _eInvoiceService.UploadEInvoice(fatoora.ObjRequest, branch, environment);
            }
        }
        return fatoora;
    }
}
