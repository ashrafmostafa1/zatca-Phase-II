using Zatca_Phase_II.Eum;

namespace Zatca_Phase_II.Models;

/// <summary>
/// A single invoice submission job placed in the outbox queue.
/// The background worker dequeues these and calls ZATCA asynchronously,
/// so the POS UI is never blocked waiting for the ZATCA response.
/// </summary>
public class InvoiceOutboxMessage
{
    /// <summary>Unique identifier of the POS bill.</summary>
    public int BillId { get; init; }

    /// <summary>Full bill object to be converted to a ZATCA invoice.</summary>
    public required Bill Bill { get; init; }

    /// <summary>Branch CSID credentials used to sign and submit the invoice.</summary>
    public required ZatcaBranch ZatcaBranch { get; init; }

    /// <summary>Target ZATCA environment (Simulation or Production).</summary>
    public EnvironmentTyp Environment { get; init; }

    /// <summary>Number of delivery attempts so far (0 = first try).</summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>UTC timestamp when this message was first enqueued.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
