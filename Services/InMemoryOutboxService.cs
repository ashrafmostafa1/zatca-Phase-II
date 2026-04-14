using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Zatca_Phase_II.Eum;
using Zatca_Phase_II.Interfaces;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Services;

public class InMemoryOutboxService : IOutboxEInvoiceService
{
    private readonly Channel<InvoiceOutboxMessage> _channel;

    public InMemoryOutboxService(ZatcaOptions options)
    {
        _channel = Channel.CreateBounded<InvoiceOutboxMessage>(new BoundedChannelOptions(options.OutboxCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public int PendingCount => _channel.Reader.Count;

    public async Task EnqueueAsync(Bill bill, ZatcaBranch branch, EnvironmentTyp environment, CancellationToken ct = default)
    {
        var message = new InvoiceOutboxMessage
        {
            BillId = bill.Id,
            Bill = bill,
            ZatcaBranch = branch,
            Environment = environment,
            CreatedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        await _channel.Writer.WriteAsync(message, ct);
    }

    public async IAsyncEnumerable<InvoiceOutboxMessage> ConsumeAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var message in _channel.Reader.ReadAllAsync(ct))
        {
            yield return message;
        }
    }

    public void CompleteAdding()
    {
        _channel.Writer.Complete();
    }
}
