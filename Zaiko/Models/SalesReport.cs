namespace Zaiko.Models;

public class SalesReport : AuditableEntity
{
    public int SalesReportId { get; set; }
    public int ClientId { get; set; }
    public int ProductId { get; set; }
    public string YearMonth { get; set; } = string.Empty;
    public int ClosingStock { get; set; }

    public Client Client { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
