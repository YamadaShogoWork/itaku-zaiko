namespace Zaiko.Services;

public class ExcelReportData
{
    public string ClientName { get; set; } = string.Empty;
    public string YearMonth { get; set; } = string.Empty;  // "YYYY-MM"
    public List<DateOnly> DeliveryDates { get; set; } = [];  // up to 4 (IsCarryOver=false)
    public List<ExcelReportRow> Rows { get; set; } = [];
    public bool IsDeliveryReport { get; set; }  // true = blank 期末在庫/売上点数/売上額
}

public class ExcelReportRow
{
    public string ProductName { get; set; } = string.Empty;
    public string? ColorName { get; set; }
    public int RetailPrice { get; set; }
    public decimal CommissionRate { get; set; }
    public int CarryOverQuantity { get; set; }  // 期首在庫数
    public Dictionary<DateOnly, int> DeliveryQuantities { get; set; } = [];  // 日付 → 数量
    public int? ClosingStock { get; set; }  // null = 空欄（納品時出力）
}
