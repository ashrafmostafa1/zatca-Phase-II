using System;
using Zatca_Phase_II.Eum;

namespace Zatca_Phase_II.Models;

public class InvoiceOutboxMessage
{
    public int BillId { get; set; }
    public Bill Bill { get; set; } = null!;
    public ZatcaBranch ZatcaBranch { get; set; } = null!;
    public EnvironmentTyp Environment { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
