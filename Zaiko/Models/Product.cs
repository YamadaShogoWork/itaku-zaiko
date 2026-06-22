namespace Zaiko.Models;

public class Product : AuditableEntity
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int? ColorId { get; set; }
    public int? SizeId { get; set; }
    public int RetailPrice { get; set; }
    public decimal CommissionRate { get; set; }

    public Color? Color { get; set; }
    public Size? Size { get; set; }
    public ICollection<Delivery> Deliveries { get; set; } = [];
    public ICollection<SalesReport> SalesReports { get; set; } = [];
    public ICollection<ClientProduct> ClientProducts { get; set; } = [];
}
