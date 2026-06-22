namespace Zaiko.Models;

public class ClientProduct : AuditableEntity
{
    public int ClientProductId { get; set; }
    public int ClientId { get; set; }
    public int ProductId { get; set; }
    public decimal CommissionRate { get; set; }

    public Client Client { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
