using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Zatca_Phase_II.Eum;
using Zatca_Phase_II.Helpers;
using Zatca_Phase_II.Interfaces;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Services;

/// <summary>
/// In-memory outbox backed by a bounded <see cref="Channel{T}"/>.
///
/// The POS calls <see cref="EnqueueAsync"/> immediately after the bill is saved
/// to the database — this returns in microseconds.  The <see cref="ZatcaBackgroundWorker"/>
/// drains the channel and talks to ZATCA in the background.
///
/// If the channel is full (<see cref="ZatcaOptions.OutboxCapacity"/> reached) the caller
/// blocks until space is available, providing natural back-pressure.
/// </summary>
public sealed class InMemoryOutboxService : IOutboxEInvoiceService
{
    private readonly Channel<InvoiceOutboxMessage> _channel;

    public InMemoryOutboxService(IOptions<ZatcaOptions> options)
        : this(options.Value) { }

    public InMemoryOutboxService(ZatcaOptions options)
    {
        _channel = Channel.CreateBounded<InvoiceOutboxMessage>(
            new BoundedChannelOptions(options.OutboxCapacity)
            {
                FullMode            = BoundedChannelFullMode.Wait,
                SingleReader        = true,   // only the background worker reads
                SingleWriter        = false,  // multiple POS threads may write
                AllowSynchronousContinuations = false,
            }
        );
    }

    /// <inheritdoc/>
    public ValueTask EnqueueAsync(
        Bill bill,
        ZatcaBranch branch,
        EnvironmentTyp environment,
        CancellationToken ct = default
    )
    {
        var message = new InvoiceOutboxMessage
        {
            BillId      = bill.Id,
            Bill        = bill,
            ZatcaBranch = branch,
            Environment = environment,
        };

        LogsFile.MessageZatca(
            $"[Outbox] Enqueued bill #{bill.Id} (UUID: {bill.Uid}). " +
            $"Pending: {_channel.Reader.Count + 1}"
        );

        return _channel.Writer.WriteAsync(message, ct);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<InvoiceOutboxMessage> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        await foreach (var msg in _channel.Reader.ReadAllAsync(ct))
        {
            yield return msg;
        }
    }

    /// <inheritdoc/>
    public int PendingCount => _channel.Reader.Count;

    /// <summary>
    /// Signals that no more messages will be written.
    /// Call this during application shutdown to allow the background worker to drain cleanly.
    /// </summary>
    public void CompleteAdding() => _channel.Writer.TryComplete();
}
