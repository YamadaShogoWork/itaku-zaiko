namespace Zaiko.Models;

public class Delivery : AuditableEntity
{
    public int DeliveryId { get; set; }
    public int ClientId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public DateOnly DeliveredAt { get; set; }
    public bool IsCarryOver { get; set; }

    public Client Client { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
