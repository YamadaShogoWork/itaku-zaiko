using Microsoft.AspNetCore.Mvc.Rendering;

namespace Zaiko.ViewModels;

public class DeliveryHistoryViewModel
{
    public int? ClientId { get; set; }
    public string? YearMonth { get; set; }  // "yyyy-MM"
    public List<SelectListItem> Clients { get; set; } = [];
    public List<DeliveryHistoryRow> Rows { get; set; } = [];
    public bool HasSearch { get; set; }
}

public class DeliveryHistoryRow
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public DateOnly DeliveredAt { get; set; }
    public bool IsCarryOver { get; set; }
    public int ProductCount { get; set; }
    public int TotalQuantity { get; set; }
}
