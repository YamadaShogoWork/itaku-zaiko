namespace Zaiko.ViewModels;

public class ClientProductRowViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ColorName { get; set; }
    public int RetailPrice { get; set; }
    public bool IsChecked { get; set; }
    public decimal CommissionRate { get; set; }
    public int SortOrder { get; set; }
}
