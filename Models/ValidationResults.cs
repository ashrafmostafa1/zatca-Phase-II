namespace Zatca_Phase_II.Models;

public class ValidationResults
{
    public InfoMessage InfoMessages { get; set; } = new InfoMessage();
    public List<object> WarningMessages { get; set; } = new List<object>();
    public List<object> ErrorMessages { get; set; } = new List<object>();
    public string Status { get; set; } = string.Empty;
}
