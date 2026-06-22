namespace Zaiko.Models;

public class Color : AuditableEntity
{
    public int ColorId { get; set; }
    public string ColorName { get; set; } = string.Empty;

    public ICollection<Product> Products { get; set; } = [];
}
