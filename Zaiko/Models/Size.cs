namespace Zaiko.Models;

public class Size : AuditableEntity
{
    public int SizeId { get; set; }
    public string SizeName { get; set; } = string.Empty;

    public ICollection<Product> Products { get; set; } = [];
}
