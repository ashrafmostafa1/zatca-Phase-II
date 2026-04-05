using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zatca_Phase_II.Interfaces;
using Zatca_Phase_II.Models;
using Zatca_Phase_II.Services;

namespace Zatca_Phase_II.Helpers;

/// <summary>
/// <see cref="IServiceCollection"/> extension that registers all ZATCA services
/// in a single, maintainable call.
///
/// Typical usage in <c>Program.cs</c> / <c>Startup.cs</c>:
/// <code>
/// builder.Services.AddZatcaServices(o =>
/// {
///     o.IsSimulation   = builder.Environment.IsDevelopment();
///     o.MaxRetries     = 3;
///     o.RetryDelayMs   = 1_000;
///     o.TimeoutSeconds = 30;
///     o.OutboxCapacity = 500;
/// });
/// </code>
///
/// Or bind from <c>appsettings.json</c>:
/// <code>
/// builder.Services.AddZatcaServices(
///     builder.Configuration.GetSection(ZatcaOptions.SectionName)
/// );
/// </code>
/// </summary>
public static class ServiceCollectionExtensions
{
    // ── Fluent config (Action overload) ───────────────────────────────────────

    /// <summary>
    /// Registers ZATCA services with options configured via a delegate.
    /// The <see cref="ZatcaBackgroundWorker"/> is registered automatically.
    /// </summary>
    public static IServiceCollection AddZatcaServices(
        this IServiceCollection services,
        Action<ZatcaOptions> configure
    )
    {
        services.Configure(configure);
        return RegisterCore(services);
    }

    // ── appsettings.json / IConfiguration overload ────────────────────────────

    /// <summary>
    /// Registers ZATCA services with options bound from a configuration section.
    /// </summary>
    public static IServiceCollection AddZatcaServices(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfigurationSection section
    )
    {
        services.Configure<ZatcaOptions>(section);
        return RegisterCore(services);
    }

    // ── Without background worker ─────────────────────────────────────────────

    /// <summary>
    /// Registers ZATCA services WITHOUT the background worker.
    /// Use this when the consuming app has its own background-processing pipeline.
    /// </summary>
    public static IServiceCollection AddZatcaCoreServices(
        this IServiceCollection services,
        Action<ZatcaOptions> configure
    )
    {
        services.Configure(configure);
        return RegisterCore(services, includeWorker: false);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private static IServiceCollection RegisterCore(
        IServiceCollection services,
        bool includeWorker = true
    )
    {
        // Infrastructure: HTTP API client
        services.AddSingleton<IAPIService, APIService>();

        // Application: e-invoice orchestrator
        services.AddScoped<IEInvoiceService, EInvoiceService>();

        // Outbox: singleton so all scopes share the same channel
        services.AddSingleton<IOutboxEInvoiceService, InMemoryOutboxService>();

        // Background worker (optional)
        if (includeWorker)
            services.AddHostedService<ZatcaBackgroundWorker>();

        return services;
    }
}
