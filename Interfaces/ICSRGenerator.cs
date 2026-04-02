using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Interfaces;

public interface ICSRGenerator
{
    Task<(ComplianceResponse?, string?)> GenerateCSR(CompanyData obj);
}
