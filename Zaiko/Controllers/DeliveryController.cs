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
public class DeliveryController(
    ApplicationDbContext db,
    StockCalculationService stockService,
    ExcelOutputService excelService) : Controller
{
    public async Task<IActionResult> Index(int? clientId, string? date)
    {
        var clients = await db.Clients
            .Where(c => c.IsActive)
            .OrderBy(c => c.ClientName)
            .ToListAsync();

        var vm = new DeliveryInputViewModel
        {
            ClientId = clientId,
            DeliveryDate = date ?? DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
            Clients = clients.Select(c => new SelectListItem(c.ClientName, c.ClientId.ToString())).ToList()
        };

        if (clientId.HasValue && clients.Any(c => c.ClientId == clientId.Value))
        {
            var client = clients.First(c => c.ClientId == clientId.Value);
            vm.ClientName = client.ClientName;
            vm.HasTable = true;
            vm.Groups = await BuildGroups(clientId.Value, date);
        }

        // Check if we need to trigger auto-download
        if (TempData["DownloadClientId"] is int dlClientId &&
            TempData["DownloadDate"] is string dlDate)
        {
            ViewData["DownloadClientId"] = dlClientId;
            ViewData["DownloadDate"] = dlDate;
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(DeliverySaveViewModel vm)
    {
        if (!DateOnly.TryParseExact(vm.DeliveryDate, "yyyy-MM-dd",
                null, System.Globalization.DateTimeStyles.None, out var deliveryDate))
        {
            TempData["Error"] = "納品日の形式が正しくありません。";
            return RedirectToAction(nameof(Index), new { clientId = vm.ClientId, date = vm.DeliveryDate });
        }

        var yearMonth = deliveryDate.ToString("yyyy-MM");

        // 月4件制限チェック
        var existingDates = await db.Deliveries
            .Where(d => d.ClientId == vm.ClientId && !d.IsCarryOver
                        && d.DeliveredAt.ToString("yyyy-MM") == yearMonth)
            .Select(d => d.DeliveredAt)
            .Distinct()
            .ToListAsync();

        if (existingDates.Count >= 4 && !existingDates.Contains(deliveryDate))
        {
            TempData["Error"] = "この月の納品は既に4回登録されています。帳票の列数が上限に達しているため、新たな納品日では登録できません。";
            return RedirectToAction(nameof(Index), new { clientId = vm.ClientId, date = vm.DeliveryDate });
        }

        // UPSERT / DELETE
        var existingDeliveries = await db.Deliveries
            .Where(d => d.ClientId == vm.ClientId
                        && d.DeliveredAt == deliveryDate
                        && !d.IsCarryOver)
            .ToListAsync();

        foreach (var item in vm.Items)
        {
            int qty = item.Quantity ?? 0;
            var existing = existingDeliveries.FirstOrDefault(d => d.ProductId == item.ProductId);

            if (qty >= 1)
            {
                if (existing != null)
                    existing.Quantity = qty;
                else
                    db.Deliveries.Add(new Delivery
                    {
                        ClientId = vm.ClientId,
                        ProductId = item.ProductId,
                        Quantity = qty,
                        DeliveredAt = deliveryDate,
                        IsCarryOver = false
                    });
            }
            else if (existing != null)
            {
                db.Deliveries.Remove(existing);
            }
        }

        await db.SaveChangesAsync();
        TempData["Success"] = "納品を保存しました。";
        TempData["DownloadClientId"] = vm.ClientId;
        TempData["DownloadDate"] = vm.DeliveryDate;

        return RedirectToAction(nameof(Index), new { clientId = vm.ClientId, date = vm.DeliveryDate });
    }

    public async Task<IActionResult> DownloadExcel(int clientId, string date)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd",
                null, System.Globalization.DateTimeStyles.None, out var deliveryDate))
            return BadRequest();

        var client = await db.Clients.FindAsync(clientId);
        if (client == null) return NotFound();

        var yearMonth = deliveryDate.ToString("yyyy-MM");
        var data = await BuildExcelData(client.ClientName, yearMonth, isDeliveryReport: true);

        var bytes = excelService.GenerateReport(data);
        var fileName = excelService.BuildFileName(client.ClientName, yearMonth);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private async Task<List<DeliveryProductGroup>> BuildGroups(int clientId, string? date)
    {
        var deliveryDate = date != null && DateOnly.TryParseExact(date, "yyyy-MM-dd",
                null, System.Globalization.DateTimeStyles.None, out var d) ? d : DateOnly.FromDateTime(DateTime.Today);

        var clientProducts = await db.ClientProducts
            .Where(cp => cp.ClientId == clientId)
            .Include(cp => cp.Product).ThenInclude(p => p.Color)
            .ToListAsync();

        var targets = clientProducts.Select(cp => (cp.ClientId, cp.ProductId)).ToList();
        var stocks = await stockService.CalculateCurrentStocksAsync(targets);

        var existingQty = await db.Deliveries
            .Where(d => d.ClientId == clientId && d.DeliveredAt == deliveryDate && !d.IsCarryOver)
            .ToDictionaryAsync(d => d.ProductId, d => d.Quantity);

        var rows = clientProducts
            .OrderBy(cp => cp.Product.ProductName)
            .ThenBy(cp => cp.Product.ColorId)
            .Select(cp => new DeliveryProductRow
            {
                ProductId = cp.ProductId,
                ColorName = cp.Product.Color?.ColorName,
                CurrentStock = stocks.TryGetValue((cp.ClientId, cp.ProductId), out var s) ? s : 0,
                Quantity = existingQty.TryGetValue(cp.ProductId, out var q) ? q : null
            })
            .ToList();

        // ProductName でグルーピング
        var productNames = clientProducts
            .OrderBy(cp => cp.Product.ProductName)
            .ThenBy(cp => cp.Product.ColorId)
            .Select(cp => cp.Product.ProductName)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Join to preserve order
        var productLookup = clientProducts.ToDictionary(cp => cp.ProductId, cp => cp.Product.ProductName);

        return rows
            .GroupBy(r => productLookup[r.ProductId])
            .OrderBy(g => productNames.IndexOf(g.Key))
            .Select(g => new DeliveryProductGroup
            {
                ProductName = g.Key,
                Rows = g.ToList()
            })
            .ToList();
    }

    private async Task<ExcelReportData> BuildExcelData(string clientName, string yearMonth, bool isDeliveryReport)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.ClientName == clientName);
        if (client == null) return new ExcelReportData { ClientName = clientName, YearMonth = yearMonth };

        var clientProducts = await db.ClientProducts
            .Where(cp => cp.ClientId == client.ClientId)
            .Include(cp => cp.Product).ThenInclude(p => p.Color)
            .ToListAsync();

        var deliveries = await db.Deliveries
            .Where(d => d.ClientId == client.ClientId
                        && d.DeliveredAt.ToString("yyyy-MM") == yearMonth)
            .ToListAsync();

        var deliveryDates = deliveries
            .Where(d => !d.IsCarryOver)
            .Select(d => d.DeliveredAt)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var rows = clientProducts
            .OrderBy(cp => cp.Product.ProductName)
            .ThenBy(cp => cp.Product.ColorId)
            .Select(cp =>
            {
                var carryOver = deliveries
                    .Where(d => d.ProductId == cp.ProductId && d.IsCarryOver)
                    .Sum(d => d.Quantity);
                var deliveryQty = deliveries
                    .Where(d => d.ProductId == cp.ProductId && !d.IsCarryOver)
                    .ToDictionary(d => d.DeliveredAt, d => d.Quantity);

                return new ExcelReportRow
                {
                    ProductName = cp.Product.ProductName,
                    ColorName = cp.Product.Color?.ColorName,
                    RetailPrice = cp.Product.RetailPrice,
                    CommissionRate = cp.CommissionRate,
                    CarryOverQuantity = carryOver,
                    DeliveryQuantities = deliveryQty,
                    ClosingStock = null  // null = blank for delivery report
                };
            })
            .ToList();

        return new ExcelReportData
        {
            ClientName = clientName,
            YearMonth = yearMonth,
            DeliveryDates = deliveryDates,
            Rows = rows,
            IsDeliveryReport = isDeliveryReport
        };
    }
}
