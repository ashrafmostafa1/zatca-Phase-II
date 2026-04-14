using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zatca_Phase_II.Eum;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Interfaces;

public interface IOutboxEInvoiceService
{
    int PendingCount { get; }
    Task EnqueueAsync(Bill bill, ZatcaBranch branch, EnvironmentTyp environment, CancellationToken ct = default);
    IAsyncEnumerable<InvoiceOutboxMessage> ConsumeAsync(CancellationToken ct = default);
}
