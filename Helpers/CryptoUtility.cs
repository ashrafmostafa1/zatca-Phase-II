using System.Xml;
using System.Security.Cryptography;
using System.Text;
using ZATCA.EInvoice.SDK;

namespace Zatca_Phase_II.Helpers;

/// <summary>
/// Pure static utility class for ZATCA cryptographic operations.
///
/// Design principle: no state, no DI dependencies — fully unit-testable
/// without a running host or SDK mocks.  All methods can be validated
/// directly against the ZATCA SDK validation tool.
/// </summary>
public static class CryptoUtility
{
    // ── Invoice Hash ──────────────────────────────────────────────────────────

    /// <summary>
    /// Computes the ZATCA invoice hash (SHA-256, Base64-encoded) from a raw
    /// input string — typically the canonical XML content before signing.
    /// </summary>
    public static string ComputeSha256Base64(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Extracts the invoice hash from an already-signed <see cref="XmlDocument"/>
    /// by reading the "Generate EInvoice Hash" step value as produced by the ZATCA SDK.
    /// Returns <c>null</c> if the document has not been signed yet.
    /// </summary>
    public static string? ExtractHashFromSignedXml(XmlDocument signedXml)
    {
        ArgumentNullException.ThrowIfNull(signedXml);
        var hashGen = new EInvoiceHashGenerator();
        var result  = hashGen.GenerateEInvoiceHashing(signedXml);
        return result?.Hash;
    }

    /// <summary>
    /// Extracts the Base64-encoded QR code value from a signed invoice XML.
    /// Returns <c>null</c> if the QR node is absent.
    /// </summary>
    public static string? ExtractQrFromSignedXml(XmlDocument signedXml)
    {
        ArgumentNullException.ThrowIfNull(signedXml);
        var nsManager = new XmlNamespaceManager(signedXml.NameTable);
        nsManager.AddNamespace("cac", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
        nsManager.AddNamespace("cbc", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");

        return signedXml
            .SelectSingleNode(
                "//cac:AdditionalDocumentReference[cbc:ID='QR']/cac:Attachment/cbc:EmbeddedDocumentBinaryObject",
                nsManager
            )?.InnerText;
    }

    // ── QR — Phase 2 TLV Encoder ──────────────────────────────────────────────

    /// <summary>
    /// Encodes Phase 2 QR code fields into a TLV (Tag-Length-Value) byte array,
    /// then returns it as a Base64 string suitable for embedding in the invoice XML QR node.
    ///
    /// Tags (per ZATCA e-invoicing technical specs v3.x):
    ///   1 = Seller name (UTF-8)
    ///   2 = VAT registration number (UTF-8)
    ///   3 = Invoice date/time (UTC ISO-8601, UTF-8)
    ///   4 = Invoice total (incl. VAT) — string, UTF-8
    ///   5 = VAT total — string, UTF-8
    ///   6 = Invoice hash (Base64 SHA-256) — UTF-8
    ///   7 = ECDSA signature (Base64) — UTF-8
    ///   8 = Public key certificate (Base64 X.509) — UTF-8
    /// </summary>
    public static string BuildPhase2QrBase64(
        string sellerName,
        string vatRegistrationNumber,
        string invoiceDateTime,   // "yyyy-MM-ddTHH:mm:ssZ"
        string invoiceTotal,      // "115.00"
        string vatTotal,          // "15.00"
        string invoiceHash,       // Base64 SHA-256
        string ecdsaSignature,    // Base64
        string publicKeyCert      // Base64 X.509 DER
    )
    {
        using var ms = new MemoryStream();

        WriteTlv(ms, 1, sellerName);
        WriteTlv(ms, 2, vatRegistrationNumber);
        WriteTlv(ms, 3, invoiceDateTime);
        WriteTlv(ms, 4, invoiceTotal);
        WriteTlv(ms, 5, vatTotal);
        WriteTlv(ms, 6, invoiceHash);
        WriteTlv(ms, 7, ecdsaSignature);
        WriteTlv(ms, 8, publicKeyCert);

        return Convert.ToBase64String(ms.ToArray());
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void WriteTlv(Stream stream, byte tag, string value)
    {
        byte[] valueBytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        stream.WriteByte(tag);
        stream.WriteByte((byte)valueBytes.Length);
        stream.Write(valueBytes, 0, valueBytes.Length);
    }
}
