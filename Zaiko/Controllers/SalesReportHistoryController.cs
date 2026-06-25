using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Zaiko.Data;
using Zaiko.Services;
using Zaiko.ViewModels;

namespace Zaiko.Controllers;

[Authorize]
public class SalesReportHistoryController(
    ApplicationDbContext db,
    ExcelOutputService excelService,
    SalesReportService srService) : Controller
{
    public async Task<IActionResult> Index(int? clientId, string? yearMonth)
    {
        var clients = await db.Clients
            .OrderBy(c => c.ClientName)
            .ToListAsync();

        var vm = new SalesReportHistoryViewModel
        {
            ClientId = clientId,
            YearMonth = yearMonth ?? DateTime.Today.ToString("yyyy-MM"),
            Clients = clients.Select(c => new SelectListItem(c.ClientName, c.ClientId.ToString())).ToList(),
            HasSearch = clientId.HasValue || yearMonth != null
        };

        if (vm.HasSearch)
        {
            var targetYM = vm.YearMonth ?? DateTime.Today.ToString("yyyy-MM");

            var salesReportQuery = db.SalesReports
                .Include(sr => sr.Client)
                .Include(sr => sr.Product).ThenInclude(p => p.Color)
                .AsQueryable();

            if (clientId.HasValue)
                salesReportQuery = salesReportQuery.Where(sr => sr.ClientId == clientId.Value);

            salesReportQuery = salesReportQuery.Where(sr => sr.YearMonth == targetYM);

            var salesReports = await salesReportQuery.ToListAsync();

            // Load deliveries for this yearMonth for calculation
            var clientIds = salesReports.Select(sr => sr.ClientId).Distinct().ToList();
            var productIds = salesReports.Select(sr => sr.ProductId).Distinct().ToList();

            var tmParts = targetYM.Split('-');
            int tmYear = int.Parse(tmParts[0]);
            int tmMonth = int.Parse(tmParts[1]);

            var deliveries = await db.Deliveries
                .Where(d => clientIds.Contains(d.ClientId)
                            && productIds.Contains(d.ProductId)
                            && d.DeliveredAt.Year == tmYear && d.DeliveredAt.Month == tmMonth)
                .ToListAsync();

            var clientProducts = await db.ClientProducts
                .Where(cp => clientIds.Contains(cp.ClientId) && productIds.Contains(cp.ProductId))
                .ToListAsync();
            var cpDict = clientProducts.ToDictionary(cp => (cp.ClientId, cp.ProductId), cp => cp.CommissionRate);

            // Check which (ClientId, YearMonth) groups have a next-month SalesReport
            var nextYM = GetNextYearMonth(targetYM);
            var lockedClientIds = await db.SalesReports
                .Where(sr => clientIds.Contains(sr.ClientId) && sr.YearMonth == nextYM)
                .Select(sr => sr.ClientId)
                .Distinct()
                .ToHashSetAsync();

            vm.Rows = salesReports
                .GroupBy(sr => (sr.ClientId, sr.YearMonth))
                .OrderByDescending(g => g.Key.YearMonth)
                .ThenBy(g => g.Key.ClientId)
                .Select(g =>
                {
                    int totalSalesQty = 0;
                    decimal totalSalesAmount = 0;

                    foreach (var sr in g)
                    {
                        var carryOver = deliveries
                            .Where(d => d.ClientId == sr.ClientId && d.ProductId == sr.ProductId && d.IsCarryOver)
                            .Sum(d => d.Quantity);
                        var delivTotal = deliveries
                            .Where(d => d.ClientId == sr.ClientId && d.ProductId == sr.ProductId && !d.IsCarryOver)
                            .Sum(d => d.Quantity);
                        int salesQty = carryOver + delivTotal - sr.ClosingStock;
                        cpDict.TryGetValue((sr.ClientId, sr.ProductId), out var rate);
                        decimal wholesale = (sr.Product?.RetailPrice ?? 0) * rate;
                        totalSalesQty += salesQty;
                        totalSalesAmount += salesQty * wholesale;
                    }

                    bool isLocked = lockedClientIds.Contains(g.Key.ClientId);
                    return new SalesReportHistoryRow
                    {
                        ClientId = g.Key.ClientId,
                        ClientName = g.First().Client.ClientName,
                        YearMonth = g.Key.YearMonth,
                        ProductCount = g.Count(),
                        TotalSalesQty = totalSalesQty,
                        TotalSalesAmount = totalSalesAmount,
                        CanEdit = !isLocked,
                        CanDelete = !isLocked
                    };
                })
                .ToList();
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int clientId, string yearMonth, string? returnYearMonth)
    {
        // Check next-month lock
        var nextYM = GetNextYearMonth(yearMonth);
        bool isLocked = await db.SalesReports
            .AnyAsync(sr => sr.ClientId == clientId && sr.YearMonth == nextYM);
        if (isLocked)
        {
            TempData["Error"] = "翌月分が登録済みのため削除できません。";
            return RedirectToAction(nameof(Index), new { clientId, yearMonth = returnYearMonth });
        }

        var toDelete = await db.SalesReports
            .Where(sr => sr.ClientId == clientId && sr.YearMonth == yearMonth)
            .ToListAsync();
        db.SalesReports.RemoveRange(toDelete);
        await db.SaveChangesAsync();

        TempData["Success"] = "売上報告を削除しました。";
        return RedirectToAction(nameof(Index), new { clientId, yearMonth = returnYearMonth });
    }

    public async Task<IActionResult> Preview(int clientId, string yearMonth)
    {
        var client = await db.Clients.FindAsync(clientId);
        if (client == null) return NotFound();

        var data = await srService.BuildExcelDataAsync(clientId, client.ClientName, yearMonth, client.FaxNumber);

        var rows = data.Rows.Select(r =>
        {
            int closingStock = r.ClosingStock ?? 0;
            return new SalesReportPreviewRow
            {
                ProductName = r.ProductName,
                ColorName = r.ColorName,
                RetailPrice = r.RetailPrice,
                CommissionRate = r.CommissionRate,
                CarryOverQuantity = r.CarryOverQuantity,
                DeliveryByDate = r.DeliveryQuantities,
                DeliveryTotal = r.DeliveryQuantities.Values.Sum(),
                ClosingStock = closingStock
            };
        }).ToList();

        var vm = new SalesReportPreviewViewModel
        {
            ClientId = clientId,
            ClientName = client.ClientName,
            YearMonth = yearMonth,
            DeliveryDates = data.DeliveryDates,
            Rows = rows
        };

        return PartialView("_PreviewContent", vm);
    }

    public async Task<IActionResult> DownloadExcel(int clientId, string yearMonth)
    {
        var client = await db.Clients.FindAsync(clientId);
        if (client == null) return NotFound();

        var data = await srService.BuildExcelDataAsync(clientId, client.ClientName, yearMonth, client.FaxNumber);
        var bytes = excelService.GenerateReport(data);
        var fileName = excelService.BuildFileName(client.ClientName, yearMonth);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static string GetNextYearMonth(string yearMonth)
    {
        var parts = yearMonth.Split('-');
        int year = int.Parse(parts[0]);
        int month = int.Parse(parts[1]);
        month++;
        if (month > 12) { month = 1; year++; }
        return $"{year:D4}-{month:D2}";
    }
}
