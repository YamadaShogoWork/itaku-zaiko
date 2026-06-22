using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaiko.Data;
using Zaiko.Services;
using Zaiko.ViewModels;

namespace Zaiko.Controllers;

[Authorize]
public class DashboardController(
    ApplicationDbContext db,
    StockCalculationService stockService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var currentYM = today.ToString("yyyy-MM");

        var vm = new DashboardViewModel
        {
            CurrentYearMonth = currentYM
        };

        // Active client count
        vm.ActiveClientCount = await db.Clients.CountAsync(c => c.IsActive);

        // Monthly delivery count (current month, IsCarryOver=false, by client×date groups)
        var monthlyDeliveries = await db.Deliveries
            .Where(d => !d.IsCarryOver && d.DeliveredAt.Year == today.Year && d.DeliveredAt.Month == today.Month)
            .Select(d => new { d.ClientId, d.DeliveredAt })
            .Distinct()
            .CountAsync();
        vm.MonthlyDeliveryCount = monthlyDeliveries;

        // Sales YearMonth: latest registered SalesReport.YearMonth
        var latestYM = await db.SalesReports.MaxAsync(sr => (string?)sr.YearMonth);
        var salesYM = latestYM ?? currentYM;
        vm.SalesYearMonth = salesYM;

        // Sales data for latest YearMonth
        var salesData = await BuildSalesData(salesYM);
        vm.TotalSalesAmount = salesData.Values.Sum(v => v.totalAmount);

        // Previous month comparison
        var prevYM = GetPrevYearMonth(salesYM);
        var prevSalesData = await BuildSalesData(prevYM);
        decimal prevTotal = prevSalesData.Values.Sum(v => v.totalAmount);
        vm.PrevMonthSalesAmount = prevTotal > 0 ? prevTotal : null;

        // Sales ranking (top 4 + others)
        var rankingByClient = salesData
            .GroupBy(kv => kv.Value.clientName)
            .Select(g => new { ClientName = g.Key, Amount = g.Sum(kv => kv.Value.totalAmount) })
            .OrderByDescending(x => x.Amount)
            .ToList();

        decimal maxAmount = rankingByClient.FirstOrDefault()?.Amount ?? 0;
        var top4 = rankingByClient.Take(4).ToList();
        var others = rankingByClient.Skip(4).ToList();

        vm.SalesRanking = top4.Select(x => new SalesRankingRow
        {
            ClientName = x.ClientName,
            SalesAmount = x.Amount,
            BarWidthPercent = maxAmount > 0 ? (double)(x.Amount / maxAmount) * 100 : 0
        }).ToList();

        if (others.Any())
        {
            decimal othersAmount = others.Sum(x => x.Amount);
            vm.SalesRanking.Add(new SalesRankingRow
            {
                ClientName = "その他",
                SalesAmount = othersAmount,
                IsOther = true,
                BarWidthPercent = maxAmount > 0 ? (double)(othersAmount / maxAmount) * 100 : 0
            });
        }

        // Stock alerts
        var activeClientProducts = await db.ClientProducts
            .Where(cp => cp.Client.IsActive)
            .Include(cp => cp.Product).ThenInclude(p => p.Color)
            .Include(cp => cp.Client)
            .ToListAsync();

        var targets = activeClientProducts
            .Select(cp => (cp.ClientId, cp.ProductId))
            .Distinct()
            .ToList();

        var stocks = await stockService.CalculateCurrentStocksAsync(targets);

        var alerts = activeClientProducts
            .Select(cp =>
            {
                stocks.TryGetValue((cp.ClientId, cp.ProductId), out var stock);
                string productDisplay = cp.Product.ProductName;
                if (cp.Product.Color != null)
                    productDisplay += " " + cp.Product.Color.ColorName;
                return new { cp, stock, productDisplay };
            })
            .Where(x => x.stock >= 1 && x.stock <= 5)
            .OrderBy(x => x.stock)
            .ToList();

        vm.AlertCount = alerts.Count;
        vm.DangerCount = alerts.Count(x => x.stock <= 2);

        vm.StockAlerts = alerts.Take(5).Select(x => new StockAlertRow
        {
            ProductDisplay = x.productDisplay,
            ClientName = x.cp.Client.ClientName,
            CurrentStock = x.stock,
            IsDanger = x.stock <= 2
        }).ToList();

        // Recent deliveries (last 5 groups)
        var recentDeliveries = await db.Deliveries
            .Include(d => d.Client)
            .OrderByDescending(d => d.DeliveredAt)
            .ThenByDescending(d => d.CreatedAt)
            .ToListAsync();

        vm.RecentDeliveries = recentDeliveries
            .GroupBy(d => (d.ClientId, d.DeliveredAt, d.IsCarryOver))
            .OrderByDescending(g => g.Key.DeliveredAt)
            .ThenByDescending(g => g.Max(d => d.CreatedAt))
            .Take(5)
            .Select(g => new RecentDeliveryRow
            {
                DeliveredAt = g.Key.DeliveredAt,
                ClientName = g.First().Client.ClientName,
                ProductCount = g.Count(),
                TotalQuantity = g.Sum(d => d.Quantity),
                IsCarryOver = g.Key.IsCarryOver
            })
            .ToList();

        return View(vm);
    }

    private async Task<Dictionary<int, (string clientName, decimal totalAmount)>> BuildSalesData(string yearMonth)
    {
        var salesReports = await db.SalesReports
            .Where(sr => sr.YearMonth == yearMonth)
            .Include(sr => sr.Client)
            .ToListAsync();

        if (!salesReports.Any())
            return [];

        var clientIds = salesReports.Select(sr => sr.ClientId).Distinct().ToList();
        var productIds = salesReports.Select(sr => sr.ProductId).Distinct().ToList();

        var parts = yearMonth.Split('-');
        int ymYear = int.Parse(parts[0]);
        int ymMonth = int.Parse(parts[1]);

        var deliveries = await db.Deliveries
            .Where(d => clientIds.Contains(d.ClientId)
                        && productIds.Contains(d.ProductId)
                        && d.DeliveredAt.Year == ymYear && d.DeliveredAt.Month == ymMonth)
            .ToListAsync();

        var clientProducts = await db.ClientProducts
            .Where(cp => clientIds.Contains(cp.ClientId) && productIds.Contains(cp.ProductId))
            .Include(cp => cp.Product)
            .ToListAsync();
        var cpDict = clientProducts.ToDictionary(cp => (cp.ClientId, cp.ProductId));

        var result = new Dictionary<int, (string, decimal)>();
        foreach (var sr in salesReports)
        {
            var carryOver = deliveries
                .Where(d => d.ClientId == sr.ClientId && d.ProductId == sr.ProductId && d.IsCarryOver)
                .Sum(d => d.Quantity);
            var delivTotal = deliveries
                .Where(d => d.ClientId == sr.ClientId && d.ProductId == sr.ProductId && !d.IsCarryOver)
                .Sum(d => d.Quantity);
            int salesQty = carryOver + delivTotal - sr.ClosingStock;

            if (!cpDict.TryGetValue((sr.ClientId, sr.ProductId), out var cp)) continue;
            decimal wholesale = cp.Product.RetailPrice * cp.CommissionRate;
            decimal amount = salesQty * wholesale;

            if (result.TryGetValue(sr.ClientId, out var existing))
                result[sr.ClientId] = (existing.Item1, existing.Item2 + amount);
            else
                result[sr.ClientId] = (sr.Client.ClientName, amount);
        }

        return result;
    }

    private static string GetPrevYearMonth(string yearMonth)
    {
        var parts = yearMonth.Split('-');
        int year = int.Parse(parts[0]);
        int month = int.Parse(parts[1]);
        month--;
        if (month < 1) { month = 12; year--; }
        return $"{year:D4}-{month:D2}";
    }
}
