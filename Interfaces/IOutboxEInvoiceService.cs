using Zatca_Phase_II.Eum;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Interfaces;

/// <summary>
/// Outbox pattern interface for fire-and-forget ZATCA invoice submission.
/// The POS enqueues bills here immediately after saving to the database,
/// then the <c>ZatcaBackgroundWorker</c> processes them asynchronously
/// so the cashier never has to wait for the ZATCA API response.
/// </summary>
public interface IOutboxEInvoiceService
{
    /// <summary>
    /// Enqueues a bill for background delivery to ZATCA.
    /// Returns immediately — the caller does not wait for ZATCA.
    /// </summary>
    /// <param name="bill">The bill to submit.</param>
    /// <param name="branch">The ZATCA branch credentials.</param>
    /// <param name="environment">Simulation or Production.</param>
    /// <param name="ct">Optional cancellation token.</param>
    ValueTask EnqueueAsync(
        Bill bill,
        ZatcaBranch branch,
        EnvironmentTyp environment,
        CancellationToken ct = default
    );

    /// <summary>
    /// Returns an async stream of messages for the background worker to process.
    /// Blocks until a message is available or <paramref name="ct"/> is cancelled.
    /// </summary>
    IAsyncEnumerable<InvoiceOutboxMessage> ConsumeAsync(CancellationToken ct);

    /// <summary>Returns the number of messages currently waiting in the outbox.</summary>
    int PendingCount { get; }
}
