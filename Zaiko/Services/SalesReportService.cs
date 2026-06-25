using Microsoft.EntityFrameworkCore;
using Zaiko.Data;
using Zaiko.Services;

namespace Zaiko.Services;

public class SalesReportService(ApplicationDbContext db)
{
    public async Task<ExcelReportData> BuildExcelDataAsync(int clientId, string clientName, string yearMonth, string? faxNumber = null)
    {
        var clientProducts = await db.ClientProducts
            .Where(cp => cp.ClientId == clientId)
            .Include(cp => cp.Product).ThenInclude(p => p.Color)
            .ToListAsync();

        var parts = yearMonth.Split('-');
        int srYear = int.Parse(parts[0]);
        int srMonth = int.Parse(parts[1]);

        var deliveries = await db.Deliveries
            .Where(d => d.ClientId == clientId
                        && d.DeliveredAt.Year == srYear && d.DeliveredAt.Month == srMonth)
            .ToListAsync();

        var salesReports = await db.SalesReports
            .Where(sr => sr.ClientId == clientId && sr.YearMonth == yearMonth)
            .ToDictionaryAsync(sr => sr.ProductId, sr => sr.ClosingStock);

        var deliveryDates = deliveries
            .Where(d => !d.IsCarryOver)
            .Select(d => d.DeliveredAt)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var rows = clientProducts
            .Where(cp => salesReports.ContainsKey(cp.ProductId))
            .OrderBy(cp => cp.Product.ProductName)
            .ThenBy(cp => cp.Product.ColorId)
            .Select(cp =>
            {
                var carryOver = deliveries
                    .Where(d => d.ProductId == cp.ProductId && d.IsCarryOver)
                    .Sum(d => d.Quantity);
                var byDate = deliveries
                    .Where(d => d.ProductId == cp.ProductId && !d.IsCarryOver)
                    .ToDictionary(d => d.DeliveredAt, d => d.Quantity);

                return new ExcelReportRow
                {
                    ProductName = cp.Product.ProductName,
                    ColorName = cp.Product.Color?.ColorName,
                    RetailPrice = cp.Product.RetailPrice,
                    CommissionRate = cp.CommissionRate,
                    CarryOverQuantity = carryOver,
                    DeliveryQuantities = byDate,
                    ClosingStock = salesReports[cp.ProductId]
                };
            })
            .ToList();

        return new ExcelReportData
        {
            ClientName = clientName,
            FaxNumber = faxNumber,
            YearMonth = yearMonth,
            DeliveryDates = deliveryDates,
            Rows = rows,
            IsDeliveryReport = false
        };
    }
}
