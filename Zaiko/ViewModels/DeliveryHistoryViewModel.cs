using Microsoft.AspNetCore.Mvc.Rendering;

namespace Zaiko.ViewModels;

public class DeliveryHistoryViewModel
{
    public int? ClientId { get; set; }
    public string? YearMonth { get; set; }  // "yyyy-MM"
    public List<SelectListItem> Clients { get; set; } = [];
    public List<DeliveryHistoryRow> Rows { get; set; } = [];
    public int Page { get; set; }
    public int TotalCount { get; set; }
    public const int PageSize = 100;
    public bool HasPrev => Page > 0;
    public bool HasNext => (Page + 1) * PageSize < TotalCount;
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
