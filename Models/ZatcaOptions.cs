namespace Zatca_Phase_II.Models;

public class ZatcaOptions
{
    public const string SectionName = "Zatca";
    public bool IsSimulation { get; set; } = false;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public int TimeoutSeconds { get; set; } = 30;
    public int OutboxCapacity { get; set; } = 500;
}
