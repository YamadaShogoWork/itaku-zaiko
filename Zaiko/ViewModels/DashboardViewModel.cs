namespace Zaiko.ViewModels;

public class DashboardViewModel
{
    public string CurrentYearMonth { get; set; } = string.Empty;
    public string SalesYearMonth { get; set; } = string.Empty;
    public int ActiveClientCount { get; set; }
    public int MonthlyDeliveryCount { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public decimal? PrevMonthSalesAmount { get; set; }
    public int AlertCount { get; set; }
    public int DangerCount { get; set; }
    public List<SalesRankingRow> SalesRanking { get; set; } = [];
    public List<StockAlertRow> StockAlerts { get; set; } = [];
    public List<RecentDeliveryRow> RecentDeliveries { get; set; } = [];
}

public class SalesRankingRow
{
    public string ClientName { get; set; } = string.Empty;
    public decimal SalesAmount { get; set; }
    public bool IsOther { get; set; }
    public double BarWidthPercent { get; set; }
}

public class StockAlertRow
{
    public string ProductDisplay { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public bool IsDanger { get; set; }
}

public class RecentDeliveryRow
{
    public DateOnly DeliveredAt { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public int TotalQuantity { get; set; }
    public bool IsCarryOver { get; set; }
}
