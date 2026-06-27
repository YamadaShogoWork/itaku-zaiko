using Microsoft.AspNetCore.Mvc.Rendering;

namespace Zaiko.ViewModels;

public class SalesReportHistoryViewModel
{
    public int? ClientId { get; set; }
    public string? YearMonth { get; set; }
    public List<SelectListItem> Clients { get; set; } = [];
    public List<SalesReportHistoryRow> Rows { get; set; } = [];
    public int Page { get; set; }
    public int TotalCount { get; set; }
    public const int PageSize = 100;
    public bool HasPrev => Page > 0;
    public bool HasNext => (Page + 1) * PageSize < TotalCount;
}

public class SalesReportHistoryRow
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string YearMonth { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public int TotalSalesQty { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public class SalesReportPreviewViewModel
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string YearMonth { get; set; } = string.Empty;
    public List<DateOnly> DeliveryDates { get; set; } = [];
    public List<SalesReportPreviewRow> Rows { get; set; } = [];
}

public class SalesReportPreviewRow
{
    public string ProductName { get; set; } = string.Empty;
    public string? ColorName { get; set; }
    public int RetailPrice { get; set; }
    public decimal CommissionRate { get; set; }
    public int CarryOverQuantity { get; set; }
    public Dictionary<DateOnly, int> DeliveryByDate { get; set; } = [];
    public int DeliveryTotal { get; set; }
    public int ClosingStock { get; set; }
}
