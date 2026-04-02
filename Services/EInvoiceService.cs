using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zatca_Phase_II.Eum;
using Zatca_Phase_II.Helpers;
using Zatca_Phase_II.Interfaces;
using Zatca_Phase_II.Models;
using Zatca_Phase_II.Services;

namespace Zatca_Phase_II.Services;

public class EInvoiceService : IEInvoiceService
{
    private readonly IHostEnvironment _hostingEnvironment;

    public EInvoiceService(IHostEnvironment hostingEnvironment)
    {
        _hostingEnvironment = hostingEnvironment;
    }

    public async Task<Fatoora?> EInvoice(Bill obj, ZatcaBranch zatcaBranch, EnvironmentTyp type)
    {
        try
        {
            ZatcaXML zatca = new(_hostingEnvironment, type);
            SignsService signsService = new(_hostingEnvironment, type == EnvironmentTyp.Simulation);

            var xml = zatca.CreateXML(obj);
            var res = await signsService.SignAndSync(
                xml,
                zatcaBranch.Csr,
                zatcaBranch.PrivateKey,
                Guid.Parse(obj.Uid),
                zatcaBranch.BinarySecurityToken,
                zatcaBranch.SecretKey
            );
            var History = new Fatoora
            {
                BillId = obj.Id,
                HashXML = res.Item1!,
                Base64QR = res.Item2!,
                Path = res.Item3!,
                ObjRequest = res.Item4!,
            };

            return History;
        }
        catch (System.Exception ex)
        {
            LogsFile.MessageZatca($"Error in EInvoice: {ex.Message}");
            return null;
        }
    }

    public async Task<TestUploadResponse?> UploadEInvoice(
        object objRequest,
        ZatcaBranch zatcaBranch,
        EnvironmentTyp type
    )
    {
        APIService aPIService = new(type == EnvironmentTyp.Simulation);

        var res = await aPIService.UploadCore(
            objRequest!,
            zatcaBranch.BinarySecurityToken,
            zatcaBranch.SecretKey
        );
        return res;
    }
}
