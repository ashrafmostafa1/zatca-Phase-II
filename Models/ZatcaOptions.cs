namespace Zatca_Phase_II.Models;

/// <summary>
/// Configuration options for the ZATCA integration layer.
/// Register via <c>services.AddZatcaServices(o => { ... })</c> or bind from appsettings.json.
/// </summary>
public class ZatcaOptions
{
    public const string SectionName = "Zatca";

    /// <summary>
    /// Use the ZATCA simulation environment instead of production.
    /// Default: false (production).
    /// </summary>
    public bool IsSimulation { get; set; } = false;

    /// <summary>
    /// Maximum number of retry attempts for transient ZATCA API failures (5xx / network errors).
    /// Default: 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay in milliseconds between retry attempts (linear back-off).
    /// Default: 1000 ms.
    /// </summary>
    public int RetryDelayMs { get; set; } = 1_000;

    /// <summary>
    /// HTTP request timeout in seconds for ZATCA API calls.
    /// Default: 30 s.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of invoice messages the in-memory outbox channel can hold
    /// before applying back-pressure.  Default: 500.
    /// </summary>
    public int OutboxCapacity { get; set; } = 500;

    // ── Derived ──────────────────────────────────────────────────────────────

    internal string BaseUrl => IsSimulation
        ? "https://gw-fatoora.zatca.gov.sa/e-invoicing/simulation"
        : "https://gw-fatoora.zatca.gov.sa/e-invoicing/core";
}
