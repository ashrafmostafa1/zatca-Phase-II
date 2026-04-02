namespace Zatca_Phase_II.Models;

public class BillDetails
{
    public double ItemSellPrice { get; set; }
    public double Qty { get; set; }
    public int Id { get; set; }
    public double NetTotal { get; set; }
    public double VATTotal { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public double DiscountTotal { get; set; }
    public bool IsDelete { get; set; }
}
