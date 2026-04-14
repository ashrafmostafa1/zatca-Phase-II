using System;

namespace Zatca_Phase_II.Services;

public class ZatcaResult
{
    public bool IsSuccess { get; set; }
    public bool IsB2B { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Base64QR { get; set; }
    public string? ClearedXml { get; set; }
    public object? ZATCAApiStatus { get; set; }

    public static ZatcaResult Success(bool isB2B, string base64Qr, string? clearedXml = null)
    {
        return new ZatcaResult
        {
            IsSuccess = true,
            IsB2B = isB2B,
            Base64QR = base64Qr,
            ClearedXml = clearedXml
        };
    }

    public static ZatcaResult Failure(string errorMessage)
    {
        return new ZatcaResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
