using System;
using Microsoft.Extensions.DependencyInjection;
using Zatca_Phase_II.Interfaces;
using Zatca_Phase_II.Models;
using Zatca_Phase_II.Services;

namespace Zatca_Phase_II.Helpers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddZatcaServices(this IServiceCollection services, Action<ZatcaOptions> configureOptions)
    {
        var options = new ZatcaOptions();
        configureOptions(options);

        services.AddSingleton(options);
        services.AddScoped<IAPIService>(sp => new APIService(options.IsSimulation));
        services.AddSingleton<IOutboxEInvoiceService, InMemoryOutboxService>();
        services.AddHostedService<ZatcaBackgroundWorker>();
        
        return services;
    }
}
