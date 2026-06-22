namespace Zaiko.ViewModels;

public class ProductGroupViewModel
{
    public string ProductName { get; set; } = string.Empty;
    public int RetailPrice { get; set; }
    public decimal CommissionRate { get; set; }
    public List<ProductColorRow> Colors { get; set; } = [];
}

public class ProductColorRow
{
    public int ProductId { get; set; }
    public int? ColorId { get; set; }
    public string? ColorName { get; set; }
    public bool HasRelatedData { get; set; }
}
