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

        wb.Style.Font.FontName = "ＭＳ Ｐゴシック";
        wb.Style.Font.FontSize = 10;

        // 印刷設定
        ws.PageSetup.Header.Center.AddText("委託販売納品書");
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        ws.PageSetup.Margins.Left = 0.58333;
        ws.PageSetup.Margins.Right = 0.29630;
        ws.PageSetup.Margins.Top = 0.59259;
        ws.PageSetup.Margins.Bottom = 0.35185;
        ws.PageSetup.Margins.Header = 0.35827;
        ws.PageSetup.Margins.Footer = 0.51181;

        var ym = ParseYearMonth(data.YearMonth);

        // --- 行1: 取引先名 + 期首/期末日 ---
        ws.Row(1).Height = 25;
        ws.Range(1, 1, 1, 4).Merge();
        var nameCell = ws.Cell(1, 1);
        nameCell.Value = $"{data.ClientName}　様";
        nameCell.Style.Font.FontSize = 14;
        nameCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Range(1, 1, 1, 4).Style.Border.BottomBorder = XLBorderStyleValues.Medium;

        if (ym.HasValue)
        {
            var startDate = new DateOnly(ym.Value.Year, ym.Value.Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);
            ws.Cell(1, 6).Value = $"期首日：{startDate:yyyy.MM.dd}〜　期末日（締め日）：{endDate:yyyy.MM.dd}";
            ws.Cell(1, 6).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        // --- 行2: FAX番号 ---
        ws.Row(2).Height = 14;
        if (!string.IsNullOrEmpty(data.FaxNumber))
        {
            ws.Cell(2, 2).Value = $"FAX.{data.FaxNumber}";
            ws.Cell(2, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        // --- 行3-4: 2段ヘッダー ---
        ws.Row(3).Height = 15;
        ws.Row(4).Height = 14;

        WriteHeader(ws, 3, 1, 4, 1, "品名");
        WriteHeader(ws, 3, 2, 4, 2, "");           // 色列（元Excelでも無ラベル）
        WriteHeader(ws, 3, 3, 4, 3, "上代\n（税込）");
        WriteHeader(ws, 3, 4, 4, 4, "下代\n（税込）");
        WriteHeader(ws, 3, 5, 4, 5, "期首\n在庫数");
        WriteHeader(ws, 3, 6, 3, 9, "期間内納品数");  // F3:I3 結合
        WriteHeader(ws, 3, 10, 4, 10, "期間内\n納品計");
        WriteHeader(ws, 3, 11, 4, 11, "期末\n在庫数");
        WriteHeader(ws, 3, 12, 4, 12, "売上\n点数");
        WriteHeader(ws, 3, 13, 4, 13, "売上額");

        // 日付列ヘッダー（行4 F-I列）
        var dates = data.DeliveryDates.OrderBy(d => d).ToList();
        for (int i = 0; i < 4; i++)
        {
            var cell = ws.Cell(4, 6 + i);
            if (i < dates.Count) cell.Value = dates[i].ToString("M/d");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ApplyThinBorder(ws.Range(4, 6 + i, 4, 6 + i));
        }

        // --- データ行（行5〜） ---
        int dataRow = 5;
        var groups = BuildGroups(data.Rows);

        foreach (var (productName, groupRows) in groups)
        {
            int groupStart = dataRow;
            int groupEnd = dataRow + groupRows.Count - 1;
            bool hasColor = groupRows.Any(r => r.ColorName != null);

            for (int i = 0; i < groupRows.Count; i++)
            {
                int r = dataRow + i;
                var item = groupRows[i];
                ws.Row(r).Height = 14;

                decimal wholesale = item.RetailPrice * item.CommissionRate;
                int delivTotal = item.DeliveryQuantities.Values.Sum();
                int? salesQty = item.ClosingStock.HasValue
                    ? item.CarryOverQuantity + delivTotal - item.ClosingStock.Value : null;
                decimal? salesAmt = salesQty.HasValue ? salesQty.Value * wholesale : null;

                if (hasColor)
                {
                    ws.Cell(r, 2).Value = item.ColorName ?? "";
                    ws.Cell(r, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell(r, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }

                SetNum(ws.Cell(r, 3), item.RetailPrice, "#,##0");
                SetNum(ws.Cell(r, 4), wholesale, "#,##0");
                SetNum(ws.Cell(r, 5), item.CarryOverQuantity);

                for (int j = 0; j < 4; j++)
                    if (j < dates.Count && item.DeliveryQuantities.TryGetValue(dates[j], out var qty))
                        SetNum(ws.Cell(r, 6 + j), qty);

                SetNum(ws.Cell(r, 10), delivTotal);

                if (item.ClosingStock.HasValue)
                {
                    SetNum(ws.Cell(r, 11), item.ClosingStock.Value);
                    SetNum(ws.Cell(r, 12), salesQty!.Value);
                    SetNum(ws.Cell(r, 13), salesAmt!.Value, "#,##0");
                }

                ApplyThinBorder(ws.Range(r, 1, r, 13));
            }

            // 品名セル: 色ありは A列を縦結合、色なしは A:B を横結合
            IXLRange nameRange = hasColor
                ? ws.Range(groupStart, 1, groupEnd, 1)
                : ws.Range(groupStart, 1, groupEnd, 2);

            if (nameRange.RowCount() > 1 || nameRange.ColumnCount() > 1)
                nameRange.Merge();

            ws.Cell(groupStart, 1).Value = productName;
            ws.Cell(groupStart, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(groupStart, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Cell(groupStart, 1).Style.Alignment.WrapText = true;

            dataRow += groupRows.Count;
        }

        // --- 合計行 ---
        int totalRow = dataRow;
        ws.Row(totalRow).Height = 16;
        ws.Range(totalRow, 1, totalRow, 2).Merge();

        ws.Cell(totalRow, 4).Value = "合計";
        ws.Cell(totalRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(totalRow, 4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        SetNum(ws.Cell(totalRow, 5), data.Rows.Sum(r => r.CarryOverQuantity));
        for (int j = 0; j < 4; j++)
            if (j < dates.Count)
                SetNum(ws.Cell(totalRow, 6 + j),
                    data.Rows.Sum(r => r.DeliveryQuantities.TryGetValue(dates[j], out var q) ? q : 0));
        SetNum(ws.Cell(totalRow, 10), data.Rows.Sum(r => r.DeliveryQuantities.Values.Sum()));

        bool anyClosing = data.Rows.Any(r => r.ClosingStock.HasValue);
        if (anyClosing)
        {
            SetNum(ws.Cell(totalRow, 11), data.Rows.Sum(r => r.ClosingStock ?? 0));
            SetNum(ws.Cell(totalRow, 12), data.Rows.Sum(r =>
                r.ClosingStock.HasValue
                    ? r.CarryOverQuantity + r.DeliveryQuantities.Values.Sum() - r.ClosingStock.Value : 0));
            SetNum(ws.Cell(totalRow, 13),
                data.Rows.Where(r => r.ClosingStock.HasValue).Sum(r =>
                {
                    int sq = r.CarryOverQuantity + r.DeliveryQuantities.Values.Sum() - r.ClosingStock!.Value;
                    return sq * (r.RetailPrice * r.CommissionRate);
                }), "#,##0");
        }

        ApplyThinBorder(ws.Range(totalRow, 1, totalRow, 13));

        // --- 列幅（元Excelに合わせた値） ---
        double[] colWidths = [12, 12.33, 6.16, 5.83, 6, 5, 5, 5, 5, 6, 6, 4.66, 8.83];
        for (int i = 0; i < colWidths.Length; i++)
            ws.Column(i + 1).Width = colWidths[i];

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ヘッダーセルを書き込み（必要なら結合）
    private static void WriteHeader(IXLWorksheet ws, int r1, int c1, int r2, int c2, string text)
    {
        var range = ws.Range(r1, c1, r2, c2);
        if (r1 != r2 || c1 != c2) range.Merge();
        var cell = ws.Cell(r1, c1);
        cell.Value = text;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        cell.Style.Alignment.WrapText = true;
        ApplyThinBorder(range);
    }

    // 数値セルにフォーマット・右寄せを設定
    private static void SetNum(IXLCell cell, decimal value, string? format = null)
    {
        cell.Value = value;
        if (format != null) cell.Style.NumberFormat.Format = format;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void SetNum(IXLCell cell, int value, string? format = null)
        => SetNum(cell, (decimal)value, format);

    private static void ApplyThinBorder(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    // 挿入順を保ったまま ProductName でグルーピング
    private static List<(string Name, List<ExcelReportRow> Rows)> BuildGroups(List<ExcelReportRow> rows)
    {
        var result = new List<(string Name, List<ExcelReportRow> Rows)>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!index.TryGetValue(row.ProductName, out int i))
            {
                i = result.Count;
                result.Add((row.ProductName, []));
                index[row.ProductName] = i;
            }
            result[i].Rows.Add(row);
        }
        return result;
    }

    private static DateOnly? ParseYearMonth(string yearMonth)
    {
        if (DateOnly.TryParseExact(yearMonth + "-01", "yyyy-MM-dd",
                null, System.Globalization.DateTimeStyles.None, out var d))
            return d;
        return null;
    }
}
