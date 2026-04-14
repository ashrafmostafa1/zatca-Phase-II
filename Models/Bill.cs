using Zatca_Phase_II.Eum;

namespace Zatca_Phase_II.Models;

public class Bill
{
    public int Id { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public string Code { get; set; } = string.Empty;
    public int OrderNo { get; set; }
    public string DateOpen { get; set; } = string.Empty;
    public string RegisterDate { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public double TotalQty { get; set; }
    public double SupTotal { get; set; }
    public double TotalVAT { get; set; }
    public string DiscountValue { get; set; } = string.Empty;
    public double DiscountTotal { get; set; }
    public double DiscountTotalItems { get; set; }
    public double TotalAfterDiscount { get; set; }
    public string TobaccoTaxValue { get; set; } = string.Empty;
    public double TobaccoTaxTotal { get; set; }
    public double DeliveryFee { get; set; }
    public double NetTotal { get; set; }
    public double PaiedCash { get; set; }
    public double PaiedVisa { get; set; }
    public double Remaining { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public bool VatIsActive { get; set; }
    public string CauseDeletion { get; set; } = string.Empty;
    public bool VatIsTaxesIncluded { get; set; }
    public double VatValue { get; set; }
    public ICollection<BillDetails> BillDetails { get; set; } = new List<BillDetails>();
    public bool IsDebit { get; set; } = false;
    public string Uid { get; set; } = string.Empty;
    public CompanyData Company { get; set; } = new CompanyData();

    public NoticeType NoticeType { get; set; } = NoticeType.Regular;
    public string ParentCode { get; set; } = string.Empty;
    public CustomerORSupplier Customer { get; set; } = new CustomerORSupplier();
    public CustomerORSupplier Supplier { get; set; } = new CustomerORSupplier();
    public string PreviousInvoiceHash { get; set; } = string.Empty;
}
