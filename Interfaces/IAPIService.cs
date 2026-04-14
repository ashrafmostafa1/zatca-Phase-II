using System.Threading.Tasks;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Interfaces;

public interface IAPIService
{
    Task<ComplianceResponse?> SubmitComplianceRequestAsync(string csr, string otp);
    Task<ComplianceResponse?> SubmitProductionRequestAsync(string compliance_request_id, string _username, string _password);
    Task<ComplianceResponse?> RenewProductionCsidAsync(string csr, string otp, string _username, string _password);
    Task<TestUploadResponse?> UploadInvTest(object request, string _username, string _password);
    Task<TestUploadResponse?> UploadClearance(object request, string _username, string _password);
    Task<TestUploadResponse?> UploadCore(object request, string _username, string _password);
}
