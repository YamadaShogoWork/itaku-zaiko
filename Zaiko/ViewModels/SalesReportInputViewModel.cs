using Microsoft.AspNetCore.Mvc.Rendering;

namespace Zaiko.ViewModels;

public class SalesReportInputViewModel
{
    public int? ClientId { get; set; }
    public string? YearMonth { get; set; }
    public List<SelectListItem> Clients { get; set; } = [];
    public bool HasTable { get; set; }
    public bool IsReadOnly { get; set; }
    public string? ClientName { get; set; }
    public List<DateOnly> DeliveryDates { get; set; } = [];
    public List<SalesReportProductGroup> Groups { get; set; } = [];
}

public class SalesReportProductGroup
{
    public string ProductName { get; set; } = string.Empty;
    public List<SalesReportProductRow> Rows { get; set; } = [];
}

public class SalesReportProductRow
{
    public int ProductId { get; set; }
    public int RowIndex { get; set; }
    public string? ColorName { get; set; }
    public int RetailPrice { get; set; }
    public decimal CommissionRate { get; set; }
    public int CarryOverQuantity { get; set; }
    public Dictionary<DateOnly, int> DeliveryByDate { get; set; } = [];
    public int DeliveryTotal { get; set; }
    public int? ClosingStock { get; set; }
    public bool ClosingStockRequired { get; set; }
}

public class SalesReportItemInput
{
    public int ProductId { get; set; }
    public int? ClosingStock { get; set; }
}

public class SalesReportSaveViewModel
{
    public int ClientId { get; set; }
    public string YearMonth { get; set; } = string.Empty;
    public List<SalesReportItemInput> Items { get; set; } = [];
}
