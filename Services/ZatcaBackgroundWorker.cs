using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Zatca_Phase_II.Interfaces;

namespace Zatca_Phase_II.Services;

public class ZatcaBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public ZatcaBackgroundWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // we await the outbox messages
        using var scope = _serviceProvider.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxEInvoiceService>();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var message in outbox.ConsumeAsync(stoppingToken))
                {
                    using var innerScope = _serviceProvider.CreateScope();
                    var routingService = innerScope.ServiceProvider.GetRequiredService<ZatcaRoutingService>();
                    
                    try
                    {
                        var res = await routingService.ProcessInvoiceAsync(message.Bill, message.ZatcaBranch, message.Environment);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing invoice in background: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Task cancelled
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical error in worker: {ex.Message}");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
