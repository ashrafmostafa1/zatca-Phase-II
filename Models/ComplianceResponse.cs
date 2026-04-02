using System.Text.Json.Serialization;

namespace Zatca_Phase_II.Models;

public class ComplianceResponse
{
    [JsonPropertyName("requestID")]
    public long RequestID { get; set; }

    [JsonPropertyName("dispositionMessage")]
    public string DispositionMessage { get; set; } = string.Empty;

    [JsonPropertyName("binarySecurityToken")]
    public string BinarySecurityToken { get; set; } = string.Empty;

    [JsonPropertyName("secret")]
    public string Secret { get; set; } = string.Empty;
}
