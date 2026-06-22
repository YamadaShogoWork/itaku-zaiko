using ClosedXML.Excel;

namespace Zaiko.Services;

public class ExcelOutputService
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public string BuildFileName(string clientName, string yearMonth)
    {
        var sanitized = string.Concat(clientName.Select(c =>
            InvalidFileNameChars.Contains(c) ? '_' : c));
        var ym = yearMonth.Replace("-", "");
        return $"委託販売納品書_{sanitized}_{ym}.xlsx";
    }

    public byte[] GenerateReport(ExcelReportData data)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("委託販売納品書");

        // --- ヘッダー ---
        var ym = ParseYearMonth(data.YearMonth);
        ws.Cell(1, 1).Value = "委託販売納品書";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = "取引先:";
        ws.Cell(2, 2).Value = data.ClientName;
        ws.Cell(3, 1).Value = "対象年月:";
        ws.Cell(3, 2).Value = ym.HasValue
            ? $"{ym.Value.Year}年{ym.Value.Month}月"
            : data.YearMonth;
        ws.Cell(4, 1).Value = "出力日:";
        ws.Cell(4, 2).Value = DateTime.Now.ToString("yyyy年M月d日");

        // --- 列ヘッダー ---
        int headerRow = 6;
        int col = 1;
        ws.Cell(headerRow, col++).Value = "品名";
        ws.Cell(headerRow, col++).Value = "色";
        ws.Cell(headerRow, col++).Value = "上代（税込）";
        ws.Cell(headerRow, col++).Value = "下代（税込）";
        ws.Cell(headerRow, col++).Value = "期首在庫数";

        int dateStartCol = col;
        var dates = data.DeliveryDates.OrderBy(d => d).ToList();
        for (int i = 0; i < 4; i++)
        {
            if (i < dates.Count)
                ws.Cell(headerRow, col).Value = dates[i].ToString("M/d");
            else
                ws.Cell(headerRow, col).Value = $"納品{i + 1}";
            col++;
        }

        ws.Cell(headerRow, col++).Value = "期間内納品計";
        ws.Cell(headerRow, col++).Value = "期末在庫数";
        ws.Cell(headerRow, col++).Value = "売上点数";
        ws.Cell(headerRow, col).Value = "売上額";

        // ヘッダー行スタイル
        var headerRange = ws.Range(headerRow, 1, headerRow, col);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#BA7517");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // --- データ行 ---
        int dataRow = headerRow + 1;
        foreach (var row in data.Rows)
        {
            col = 1;
            var displayName = row.ProductName;
            decimal wholesale = row.RetailPrice * row.CommissionRate;
            int deliveryTotal = row.DeliveryQuantities.Values.Sum();
            int? salesQty = row.ClosingStock.HasValue
                ? row.CarryOverQuantity + deliveryTotal - row.ClosingStock.Value
                : null;
            decimal? salesAmount = salesQty.HasValue ? salesQty.Value * wholesale : null;

            ws.Cell(dataRow, col++).Value = displayName;
            ws.Cell(dataRow, col++).Value = row.ColorName ?? "";
            ws.Cell(dataRow, col++).Value = row.RetailPrice;
            ws.Cell(dataRow, col++).Value = wholesale;
            ws.Cell(dataRow, col++).Value = row.CarryOverQuantity;

            // 期間内納品数（最大4列）
            for (int i = 0; i < 4; i++)
            {
                if (i < dates.Count && row.DeliveryQuantities.TryGetValue(dates[i], out var qty))
                    ws.Cell(dataRow, col).Value = qty;
                col++;
            }

            ws.Cell(dataRow, col++).Value = deliveryTotal;

            if (row.ClosingStock.HasValue)
            {
                ws.Cell(dataRow, col++).Value = row.ClosingStock.Value;
                ws.Cell(dataRow, col++).Value = salesQty!.Value;
                ws.Cell(dataRow, col).Value = salesAmount!.Value;
            }
            // else: leave blank

            dataRow++;
        }

        // --- 列幅自動調整 ---
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static DateOnly? ParseYearMonth(string yearMonth)
    {
        if (DateOnly.TryParseExact(yearMonth + "-01", "yyyy-MM-dd",
                null, System.Globalization.DateTimeStyles.None, out var d))
            return d;
        return null;
    }
}
