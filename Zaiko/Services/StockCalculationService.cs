using Microsoft.EntityFrameworkCore;
using Zaiko.Data;

namespace Zaiko.Services;

public class StockCalculationService(ApplicationDbContext db)
{
    public async Task<int> CalculateCurrentStockAsync(int clientId, int productId)
    {
        var result = await CalculateCurrentStocksAsync([(clientId, productId)]);
        return result.TryGetValue((clientId, productId), out var stock) ? stock : 0;
    }

    public async Task<Dictionary<(int ClientId, int ProductId), int>> CalculateCurrentStocksAsync(
        IEnumerable<(int ClientId, int ProductId)> targets)
    {
        var targetList = targets.ToList();
        if (targetList.Count == 0) return [];

        var clientIds = targetList.Select(t => t.ClientId).ToHashSet();
        var productIds = targetList.Select(t => t.ProductId).ToHashSet();

        var salesReports = await db.SalesReports
            .Where(sr => clientIds.Contains(sr.ClientId) && productIds.Contains(sr.ProductId))
            .ToListAsync();

        var deliveries = await db.Deliveries
            .Where(d => clientIds.Contains(d.ClientId) && productIds.Contains(d.ProductId))
            .ToListAsync();

        var result = new Dictionary<(int, int), int>();
        foreach (var (clientId, productId) in targetList)
        {
            var pairSalesReports = salesReports
                .Where(sr => sr.ClientId == clientId && sr.ProductId == productId)
                .ToList();

            var pairDeliveries = deliveries
                .Where(d => d.ClientId == clientId && d.ProductId == productId)
                .ToList();

            if (pairSalesReports.Count > 0)
            {
                var latestSR = pairSalesReports.MaxBy(sr => sr.YearMonth)!;
                var afterDeliveries = pairDeliveries
                    .Where(d => !d.IsCarryOver &&
                           string.Compare(
                               d.DeliveredAt.ToString("yyyy-MM"),
                               latestSR.YearMonth,
                               StringComparison.Ordinal) > 0)
                    .Sum(d => d.Quantity);
                result[(clientId, productId)] = latestSR.ClosingStock + afterDeliveries;
            }
            else
            {
                result[(clientId, productId)] = pairDeliveries.Sum(d => d.Quantity);
            }
        }

        return result;
    }
}
