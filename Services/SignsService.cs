using System;
using System.Xml;
using Microsoft.Extensions.Hosting;
using Zatca_Phase_II.Helpers;
using ZATCA.EInvoice.SDK;

namespace Zatca_Phase_II.Services;

public class SignsService(IHostEnvironment hostingEnvironment, bool isSimulation)
{
    private readonly bool IsSimulation = isSimulation;
    private readonly APIService _apiService = new APIService(isSimulation);
    private readonly IHostEnvironment _hostingEnvironment = hostingEnvironment;

    public Task<(string?, string?, string?, object?)> SignAndSync(
        XmlDocument xmlDoc,
        string Csr,
        string PrivateKey,
        Guid uuid,
        string BinarySecurityToken,
        string Secret
    )
    {
        var _IEInvoiceSigningLogic = new EInvoiceSigner();
        var _IEInvoiceHash = new EInvoiceHashGenerator();
        var _IQRValidator = new EInvoiceQRGenerator();

        var signRes = _IEInvoiceSigningLogic.SignDocument(xmlDoc, Csr, PrivateKey);
        if (signRes.IsValid)
        {
            LogsFile.MessageZatca($"Sign");
        }
        else
        {
            LogsFile.MessageZatca($"Error Sign {signRes.ErrorMessage}");
            return Task.FromResult<(string?, string?, string?, object?)>((null, null, null, null));
        }
        XmlNamespaceManager nsManager = new XmlNamespaceManager(signRes.SignedEInvoice.NameTable);
        nsManager.AddNamespace(
            "cac",
            "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
        );
        nsManager.AddNamespace(
            "cbc",
            "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
        );

        XmlNode? qrNode = signRes.SignedEInvoice.SelectSingleNode(
            "//cac:AdditionalDocumentReference[cbc:ID='QR']/cac:Attachment/cbc:EmbeddedDocumentBinaryObject",
            nsManager
        );
        var QR = qrNode?.InnerText;
        var hash = signRes
            .Steps.FirstOrDefault(x => x.StepName == "Generate EInvoice Hash")
            ?.ResultedValue;
        var xmlBase64 = FormatXML.ConvertXmlToBase64(signRes.SignedEInvoice);
        var request = new
        {
            invoiceHash = hash,
            uuid = uuid.ToString(),
            invoice = xmlBase64,
        };

        return Task.FromResult<(string?, string?, string?, object?)>(((string?)hash, (string?)QR, (string?)xmlBase64, (object?)request));
    }

    public async Task SignDefualt(
        XmlDocument xmlDoc,
        string Csr,
        string PrivateKey,
        Guid uuid,
        string BinarySecurityToken,
        string Secret
    )
    {
        var _IEInvoiceSigningLogic = new EInvoiceSigner();
        var _IEInvoiceHash = new EInvoiceHashGenerator();
        var _IQRValidator = new EInvoiceQRGenerator();

        var signRes = _IEInvoiceSigningLogic.SignDocument(xmlDoc, Csr, PrivateKey);
        if (signRes.IsValid)
        {
            var name = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss");
            string BasePath = Path.Combine(_hostingEnvironment.ContentRootPath, "XML");
            if (!Directory.Exists(BasePath))
                Directory.CreateDirectory(BasePath);

            string fullPath = Path.Combine(BasePath, $"{name}.xml");
            signRes.SaveSignedEInvoice(fullPath);
        }
        else
        {
            LogsFile.MessageZatca($"Error Sign Default {signRes.ErrorMessage}");
        }
        var request = new
        {
            invoiceHash = signRes
                .Steps.FirstOrDefault(x => x.StepName == "Generate EInvoice Hash")
                ?.ResultedValue,
            uuid = uuid.ToString(),
            invoice = FormatXML.ConvertXmlToBase64(signRes.SignedEInvoice),
        };

        var TestRes = await _apiService.UploadInvTest(request, BinarySecurityToken, Secret);
    }
}
