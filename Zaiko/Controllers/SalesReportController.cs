using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Zaiko.Data;
using Zaiko.Models;
using Zaiko.Services;
using Zaiko.ViewModels;

namespace Zaiko.Controllers;

[Authorize]
public class SalesReportController(
    ApplicationDbContext db,
    ExcelOutputService excelService,
    SalesReportService srService) : Controller
{
    public async Task<IActionResult> Index(int? clientId, string? yearMonth, string? download)
    {
        var clients = await db.Clients
            .Where(c => c.IsActive)
            .OrderBy(c => c.ClientName)
            .ToListAsync();

        var vm = new SalesReportInputViewModel
        {
            ClientId = clientId,
            YearMonth = yearMonth ?? GetDefaultYearMonth(),
            Clients = clients.Select(c => new SelectListItem(c.ClientName, c.ClientId.ToString())).ToList()
        };

        if (clientId.HasValue)
        {
            var client = clients.FirstOrDefault(c => c.ClientId == clientId.Value);
            if (client != null)
            {
                vm.ClientName = client.ClientName;

                // Default yearMonth: next month after latest report
                if (string.IsNullOrEmpty(yearMonth))
                {
                    var maxYM = await db.SalesReports
                        .Where(sr => sr.ClientId == clientId.Value)
                        .MaxAsync(sr => (string?)sr.YearMonth);
                    yearMonth = maxYM != null ? GetNextYearMonth(maxYM) : DateTime.Today.ToString("yyyy-MM");
                    vm.YearMonth = yearMonth;
                }

                vm.HasTable = true;
                await LoadTable(vm, clientId.Value, yearMonth!);
            }
        }

        if (download == "1" && clientId.HasValue && !string.IsNullOrEmpty(yearMonth))
        {
            ViewData["DownloadClientId"] = clientId.Value;
            ViewData["DownloadYearMonth"] = yearMonth;
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SalesReportSaveViewModel vm)
    {
        // Check next-month lock
        var nextYM = GetNextYearMonth(vm.YearMonth);
        bool isLocked = await db.SalesReports
            .AnyAsync(sr => sr.ClientId == vm.ClientId && sr.YearMonth == nextYM);
        if (isLocked)
        {
            TempData["Error"] = "翌月の売上報告が登録済みのため、この月の売上報告は保存できません。";
            return RedirectToAction(nameof(Index), new { clientId = vm.ClientId, yearMonth = vm.YearMonth });
        }

        // Load carryover quantities to validate required fields
        var ymPartsV = vm.YearMonth.Split('-');
        int vmYear = int.Parse(ymPartsV[0]);
        int vmMonth = int.Parse(ymPartsV[1]);

        var carryOvers = await db.Deliveries
            .Where(d => d.ClientId == vm.ClientId && d.IsCarryOver
                        && d.DeliveredAt.Year == vmYear && d.DeliveredAt.Month == vmMonth)
            .ToDictionaryAsync(d => d.ProductId, d => d.Quantity);

        var missingProducts = vm.Items
            .Where(item => carryOvers.TryGetValue(item.ProductId, out var qty) && qty > 0 && !item.ClosingStock.HasValue)
            .Select(item => item.ProductId)
            .ToList();

        if (missingProducts.Count > 0)
        {
            TempData["Error"] = "期首在庫が存在する商品の期末在庫は必須です。";
            return RedirectToAction(nameof(Index), new { clientId = vm.ClientId, yearMonth = vm.YearMonth });
        }

        // UPSERT SalesReports
        var existingSRs = await db.SalesReports
            .Where(sr => sr.ClientId == vm.ClientId && sr.YearMonth == vm.YearMonth)
            .ToListAsync();

        foreach (var item in vm.Items.Where(i => i.ClosingStock.HasValue))
        {
            var existing = existingSRs.FirstOrDefault(sr => sr.ProductId == item.ProductId);
            if (existing != null)
            {
                existing.ClosingStock = item.ClosingStock!.Value;
            }
            else
            {
                db.SalesReports.Add(new SalesReport
                {
                    ClientId = vm.ClientId,
                    ProductId = item.ProductId,
                    YearMonth = vm.YearMonth,
                    ClosingStock = item.ClosingStock!.Value
                });
            }
        }

        // Remove SalesReports for products with no closing stock input
        var productIdsWithInput = vm.Items.Where(i => i.ClosingStock.HasValue).Select(i => i.ProductId).ToHashSet();
        var toRemoveSR = existingSRs.Where(sr => !productIdsWithInput.Contains(sr.ProductId)).ToList();
        db.SalesReports.RemoveRange(toRemoveSR);

        await db.SaveChangesAsync();

        // Monthly carryover: delete existing next-month carryover, recreate
        var nextMonthFirstDay = GetFirstDayOfNextMonth(vm.YearMonth);
        var nextYMParts = nextYM.Split('-');
        int nextYear = int.Parse(nextYMParts[0]);
        int nextMonth = int.Parse(nextYMParts[1]);
        var existingCarryOvers = await db.Deliveries
            .Where(d => d.ClientId == vm.ClientId && d.IsCarryOver
                        && d.DeliveredAt.Year == nextYear && d.DeliveredAt.Month == nextMonth)
            .ToListAsync();
        db.Deliveries.RemoveRange(existingCarryOvers);

        foreach (var item in vm.Items.Where(i => i.ClosingStock.HasValue && i.ClosingStock.Value >= 1))
        {
            db.Deliveries.Add(new Delivery
            {
                ClientId = vm.ClientId,
                ProductId = item.ProductId,
                Quantity = item.ClosingStock!.Value,
                DeliveredAt = nextMonthFirstDay,
                IsCarryOver = true
            });
        }

        await db.SaveChangesAsync();

        TempData["Success"] = "売上報告を保存しました。";

        return RedirectToAction(nameof(Index), new { clientId = vm.ClientId, yearMonth = vm.YearMonth, download = "1" });
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

    private async Task LoadTable(SalesReportInputViewModel vm, int clientId, string yearMonth)
    {
        var nextYM = GetNextYearMonth(yearMonth);
        vm.IsReadOnly = await db.SalesReports
            .AnyAsync(sr => sr.ClientId == clientId && sr.YearMonth == nextYM);

        var clientProducts = await db.ClientProducts
            .Where(cp => cp.ClientId == clientId)
            .Include(cp => cp.Product).ThenInclude(p => p.Color)
            .ToListAsync();

        var ltParts = yearMonth.Split('-');
        int ltYear = int.Parse(ltParts[0]);
        int ltMonth = int.Parse(ltParts[1]);

        var deliveries = await db.Deliveries
            .Where(d => d.ClientId == clientId
                        && d.DeliveredAt.Year == ltYear && d.DeliveredAt.Month == ltMonth)
            .ToListAsync();

        var existingSRs = await db.SalesReports
            .Where(sr => sr.ClientId == clientId && sr.YearMonth == yearMonth)
            .ToDictionaryAsync(sr => sr.ProductId, sr => sr.ClosingStock);

        vm.DeliveryDates = deliveries
            .Where(d => !d.IsCarryOver)
            .Select(d => d.DeliveredAt)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        int rowIndex = 0;
        var productRows = clientProducts
            .OrderBy(cp => cp.SortOrder)
            .ThenBy(cp => cp.Product.ColorId)
            .Select(cp =>
            {
                var carryOver = deliveries
                    .Where(d => d.ProductId == cp.ProductId && d.IsCarryOver)
                    .Sum(d => d.Quantity);
                var byDate = deliveries
                    .Where(d => d.ProductId == cp.ProductId && !d.IsCarryOver)
                    .ToDictionary(d => d.DeliveredAt, d => d.Quantity);
                int delivTotal = byDate.Values.Sum();
                existingSRs.TryGetValue(cp.ProductId, out var closingStock);

                return new SalesReportProductRow
                {
                    ProductId = cp.ProductId,
                    RowIndex = rowIndex++,
                    ColorName = cp.Product.Color?.ColorName,
                    RetailPrice = cp.Product.RetailPrice,
                    CommissionRate = cp.CommissionRate,
                    CarryOverQuantity = carryOver,
                    DeliveryByDate = byDate,
                    DeliveryTotal = delivTotal,
                    ClosingStock = closingStock,
                    ClosingStockRequired = carryOver > 0
                };
            })
            .ToList();

        var productNames = clientProducts
            .OrderBy(cp => cp.Product.ProductName)
            .ThenBy(cp => cp.Product.ColorId)
            .Select(cp => cp.Product.ProductName)
            .Distinct()
            .ToList();

        var productLookup = clientProducts.ToDictionary(cp => cp.ProductId, cp => cp.Product.ProductName);

        vm.Groups = productRows
            .GroupBy(r => productLookup[r.ProductId])
            .OrderBy(g => productNames.IndexOf(g.Key))
            .Select(g => new SalesReportProductGroup
            {
                ProductName = g.Key,
                Rows = g.ToList()
            })
            .ToList();
    }

    private static string GetDefaultYearMonth()
    {
        var today = DateTime.Today;
        return (today.Day <= 15 ? today.AddMonths(-1) : today).ToString("yyyy-MM");
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

    private static DateOnly GetFirstDayOfNextMonth(string yearMonth)
    {
        var next = GetNextYearMonth(yearMonth);
        var parts = next.Split('-');
        return new DateOnly(int.Parse(parts[0]), int.Parse(parts[1]), 1);
    }
}
