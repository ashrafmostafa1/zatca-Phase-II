using Zatca_Phase_II.Eum;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Interfaces;

public interface IEInvoiceService
{
    Task<Fatoora?> EInvoice(Bill obj, ZatcaBranch zatcaBranch, EnvironmentTyp type);
    Task<TestUploadResponse?> UploadEInvoice(
        object objRequest,
        ZatcaBranch zatcaBranch,
        EnvironmentTyp type
    );
}
