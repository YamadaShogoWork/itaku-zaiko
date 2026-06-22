namespace Zaiko.Models;

public class Client : AuditableEntity
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? FaxNumber { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Delivery> Deliveries { get; set; } = [];
    public ICollection<SalesReport> SalesReports { get; set; } = [];
    public ICollection<ClientProduct> ClientProducts { get; set; } = [];
}
