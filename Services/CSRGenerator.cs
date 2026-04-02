using System;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Hosting;
using Zatca_Phase_II.Helpers;
using Zatca_Phase_II.Interfaces;
using Zatca_Phase_II.Models;
using ZATCA.EInvoice.SDK;
using ZATCA.EInvoice.SDK.Contracts;
using ZATCA.EInvoice.SDK.Contracts.Models;

namespace Zatca_Phase_II.Services;

public class CSRGenerator(IHostEnvironment hostingEnvironment, bool isSimulation) : ICSRGenerator
{
    private readonly bool IsSimulation = isSimulation;
    private readonly APIService _apiService = new APIService(isSimulation);
    private readonly SignsService _signsService = new SignsService(
        hostingEnvironment,
        isSimulation
    );
    private readonly DefaultXML _defaultXML = new DefaultXML();

    public async Task<(ComplianceResponse?, string?)> GenerateCSR(CompanyData obj)
    {
        CsrGenerationDto dto = new(
            $"{obj.Name}-{obj.Crn}-{obj.TaxNumber}",
            $"1-Easacc{obj.SystemType}|2-V4|3-{Guid.NewGuid()}",
            $"{obj.TaxNumber}",
            $"{obj.BranchName}",
            $"{obj.Name}",
            $"SA",
            $"{obj.InvoiceType}",
            $"{obj.NationalAddress}",
            $"{obj.IndustryBusinessCategory}"
        );
        LogsFile.MessageZatca($"Start Generate CSR");
        ICsrGenerator ICSR = new CsrGenerator();
        var Environment = IsSimulation ? EnvironmentType.Simulation : EnvironmentType.Production;
        // var Environment = EnvironmentType.Production;
        try
        {
            var result = ICSR.GenerateCsr(dto, Environment, false);
            if (result.IsValid)
            {
                LogsFile.MessageZatca($"is Valid CSR");
                var ret = await _apiService.SubmitComplianceRequestAsync(result.Csr, obj.Otp);
                if (ret is null)
                {
                    LogsFile.MessageZatca($"Error SubmitComplianceRequestAsync");
                    return (null, null);
                }
                var base64Token = Encoding.UTF8.GetString(
                    Convert.FromBase64String(ret.BinarySecurityToken)
                );

                var UUIDS = new
                {
                    UUID = Guid.NewGuid(),
                    DebitUUID = Guid.NewGuid(),
                    CreditUUID = Guid.NewGuid(),
                };

                var UUIDStand = new
                {
                    UUID = Guid.NewGuid(),
                    DebitUUID = Guid.NewGuid(),
                    CreditUUID = Guid.NewGuid(),
                };
                bool needDefaultXml = obj.InvoiceType == "0100" || obj.InvoiceType == "1100";
                bool needStandardXml = obj.InvoiceType == "1000" || obj.InvoiceType == "1100";
                
                if (needStandardXml)
                {
                    var xmls = _defaultXML.CreateXmlStandarDefaultUpload(obj, UUIDStand);
                    await _signsService.SignDefualt(
                        xmls.Item1,
                        base64Token,
                        result.PrivateKey,
                        UUIDStand.UUID,
                        ret.BinarySecurityToken,
                        ret.Secret
                    );
                    await _signsService.SignDefualt(
                        xmls.Item2,
                        base64Token,
                        result.PrivateKey,
                        UUIDStand.DebitUUID,
                        ret.BinarySecurityToken,
                        ret.Secret
                    );
                    await _signsService.SignDefualt(
                        xmls.Item3,
                        base64Token,
                        result.PrivateKey,
                        UUIDStand.CreditUUID,
                        ret.BinarySecurityToken,
                        ret.Secret
                    );
                }

                if (needDefaultXml)
                {
                    var xmls = _defaultXML.CreateXmlDefaultUpload(obj, UUIDS);
                    await _signsService.SignDefualt(
                        xmls.Item1,
                        base64Token,
                        result.PrivateKey,
                        UUIDS.UUID,
                        ret.BinarySecurityToken,
                        ret.Secret
                    );
                    await _signsService.SignDefualt(
                        xmls.Item2,
                        base64Token,
                        result.PrivateKey,
                        UUIDS.DebitUUID,
                        ret.BinarySecurityToken,
                        ret.Secret
                    );
                    await _signsService.SignDefualt(
                        xmls.Item3,
                        base64Token,
                        result.PrivateKey,
                        UUIDS.CreditUUID,
                        ret.BinarySecurityToken,
                        ret.Secret
                    );
                }

                
                var retProd = await _apiService.SubmitProductionRequestAsync(
                    ret.RequestID.ToString(),
                    ret.BinarySecurityToken,
                    ret.Secret
                );
                Console.WriteLine(ret.BinarySecurityToken?.Length);
                Console.WriteLine(ret.Secret?.Length);
                Console.WriteLine(ret.RequestID);

                return (retProd, result.PrivateKey);
            }
            LogsFile.MessageZatca($"Error Generate CSR {result.ErrorMessages}");
        }
        catch (System.Exception ex)
        {
            LogsFile.MessageZatca($"Exception Generate CSR {ex.Message}");
            throw;
        }
        return (null, null);
    }
}
