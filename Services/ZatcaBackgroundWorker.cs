using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Zatca_Phase_II.Eum;
using Zatca_Phase_II.Helpers;
using Zatca_Phase_II.Interfaces;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Services;

/// <summary>
/// Background worker that drains the <see cref="IOutboxEInvoiceService"/> channel
/// and submits each invoice to ZATCA.
///
/// Register via <c>services.AddZatcaServices()</c> or explicitly:
/// <code>
///   services.AddHostedService&lt;ZatcaBackgroundWorker&gt;();
/// </code>
///
/// Dead-letter policy: if a message fails after <see cref="ZatcaOptions.MaxRetries"/>
/// delivery attempts, it is logged to the dead-letter log file and dropped —
/// the POS can reconcile these manually or via a scheduled job.
/// </summary>
public sealed class ZatcaBackgroundWorker : BackgroundService
{
    private readonly IOutboxEInvoiceService _outbox;
    private readonly IEInvoiceService       _eInvoiceService;
    private readonly int                    _maxRetries;

    public ZatcaBackgroundWorker(
        IOutboxEInvoiceService outbox,
        IEInvoiceService eInvoiceService,
        IOptions<ZatcaOptions> options
    )
    {
        _outbox          = outbox;
        _eInvoiceService = eInvoiceService;
        _maxRetries      = options.Value.MaxRetries;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogsFile.MessageZatca("[ZatcaWorker] Background worker started.");

        await foreach (var msg in _outbox.ConsumeAsync(stoppingToken))
        {
            await ProcessMessageAsync(msg, stoppingToken);
        }

        LogsFile.MessageZatca("[ZatcaWorker] Background worker stopped.");
    }

    private async Task ProcessMessageAsync(
        InvoiceOutboxMessage msg,
        CancellationToken ct
    )
    {
        try
        {
            LogsFile.MessageZatca(
                $"[ZatcaWorker] Processing bill #{msg.BillId} (attempt {msg.RetryCount + 1}/{_maxRetries + 1})"
            );

            // Step 1: Generate + sign the invoice
            var fatoora = await _eInvoiceService.EInvoice(msg.Bill, msg.ZatcaBranch, msg.Environment);

            if (fatoora == null)
            {
                LogsFile.MessageZatca($"[ZatcaWorker] Signing failed for bill #{msg.BillId}. Dead-lettering.");
                WriteDeadLetter(msg, "Signing returned null");
                return;
            }

            // Step 2: Submit to the correct ZATCA endpoint
            TestUploadResponse? result;
            if (msg.Bill.InvoiceType == InvoiceType.Standard)
            {
                result = await _eInvoiceService.ClearanceEInvoice(
                    fatoora.ObjRequest, msg.ZatcaBranch, msg.Environment
                );
            }
            else
            {
                result = await _eInvoiceService.UploadEInvoice(
                    fatoora.ObjRequest, msg.ZatcaBranch, msg.Environment
                );
            }

            // Step 3: Evaluate result
            if (result == null)
            {
                await HandleRetryOrDeadLetterAsync(msg, ct, "ZATCA API returned null");
                return;
            }

            if (result.IsDuplicate)
            {
                // Already submitted — treat as success so we don't keep retrying
                LogsFile.MessageZatca(
                    $"[ZatcaWorker] Bill #{msg.BillId} was already submitted (duplicate). " +
                    "Marking as complete."
                );
                return;
            }

            bool isSuccess = (int)result.HttpStatus >= 200 && (int)result.HttpStatus <= 299;
            if (!isSuccess)
            {
                await HandleRetryOrDeadLetterAsync(
                    msg, ct,
                    $"HTTP {result.HttpStatus}: {result.RawResponse?.Substring(0, Math.Min(200, result.RawResponse.Length))}"
                );
                return;
            }

            LogsFile.MessageZatca(
                $"[ZatcaWorker] Bill #{msg.BillId} submitted successfully. " +
                $"Hash: {fatoora.HashXML}"
            );

            // ── IMPORTANT: persist fatoora.HashXML as the PreviousInvoiceHash
            //    for the NEXT invoice from this branch.
            //    Hook into your POS persistence layer here, e.g.:
            //
            //    await _branchRepository.UpdateLastInvoiceHashAsync(
            //        msg.ZatcaBranch.RequestID, fatoora.HashXML, ct
            //    );
        }
        catch (OperationCanceledException)
        {
            LogsFile.MessageZatca($"[ZatcaWorker] Processing of bill #{msg.BillId} cancelled (shutdown).");
        }
        catch (Exception ex)
        {
            LogsFile.MessageZatca($"[ZatcaWorker] Unexpected error for bill #{msg.BillId}: {ex.Message}");
            await HandleRetryOrDeadLetterAsync(msg, ct, ex.Message);
        }
    }

    private async Task HandleRetryOrDeadLetterAsync(
        InvoiceOutboxMessage msg,
        CancellationToken ct,
        string reason
    )
    {
        msg.RetryCount++;

        if (msg.RetryCount <= _maxRetries)
        {
            int delayMs = msg.RetryCount * 2_000; // exponential-ish back-off: 2s, 4s, 6s …
            LogsFile.MessageZatca(
                $"[ZatcaWorker] Bill #{msg.BillId} failed ({reason}). " +
                $"Retry {msg.RetryCount}/{_maxRetries} in {delayMs} ms."
            );
            await Task.Delay(delayMs, ct);
            await _outbox.EnqueueAsync(msg.Bill, msg.ZatcaBranch, msg.Environment, ct);
        }
        else
        {
            WriteDeadLetter(msg, reason);
        }
    }

    private static void WriteDeadLetter(InvoiceOutboxMessage msg, string reason)
    {
        var entry = $"DEAD-LETTER | BillId={msg.BillId} | UUID={msg.Bill.Uid} | " +
                    $"Retries={msg.RetryCount} | Reason={reason} | Created={msg.CreatedAt:O}";
        LogsFile.MessageZatca($"[ZatcaWorker] {entry}");

        try
        {
            string dir  = Path.Combine("Logs", "Zatca", "DeadLetters");
            string file = Path.Combine(dir, $"{DateTime.UtcNow:yyyy-MM-dd}.txt");
            Directory.CreateDirectory(dir);
            File.AppendAllText(file, $"{DateTime.UtcNow:O} {entry}{Environment.NewLine}");
        }
        catch { /* Swallow — we're already in an error path */ }
    }
}
