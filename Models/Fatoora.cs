using System;

namespace Zatca_Phase_II.Models;

public class Fatoora
{
    public int BillId { get; set; }
    public string HashXML { get; set; } = string.Empty;
    public string Base64QR { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public required object ObjRequest { get; set; }
}
