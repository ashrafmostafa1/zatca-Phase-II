namespace Zatca_Phase_II.Models;

public class ZatcaBranch
{
    public required string RequestID { get; set; }
    public required string Csr { get; set; }
    public required string PrivateKey { get; set; }
    public required string SecretKey { get; set; }
    public required string BinarySecurityToken { get; set; }
}
