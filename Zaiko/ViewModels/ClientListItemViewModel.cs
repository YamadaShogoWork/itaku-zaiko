namespace Zaiko.ViewModels;

public class ClientListItemViewModel
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? FaxNumber { get; set; }
    public bool IsActive { get; set; }
    public int ClientProductCount { get; set; }
}
