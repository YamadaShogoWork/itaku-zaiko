using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Zaiko.Data;
using Zaiko.ViewModels;

namespace Zaiko.Controllers;

[Authorize]
public class DeliveryHistoryController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index(int? clientId, string? yearMonth, int page = 0)
    {
        var clients = await db.Clients
            .OrderBy(c => c.ClientName)
            .ToListAsync();

        var vm = new DeliveryHistoryViewModel
        {
            ClientId = clientId,
            YearMonth = yearMonth,
            Clients = clients.Select(c => new SelectListItem(c.ClientName, c.ClientId.ToString())).ToList(),
            Page = page
        };

        var query = db.Deliveries.Include(d => d.Client).AsQueryable();

        if (clientId.HasValue)
            query = query.Where(d => d.ClientId == clientId.Value);

        if (!string.IsNullOrEmpty(yearMonth))
        {
            var dhParts = yearMonth.Split('-');
            int dhYear = int.Parse(dhParts[0]);
            int dhMonth = int.Parse(dhParts[1]);
            query = query.Where(d => d.DeliveredAt.Year == dhYear && d.DeliveredAt.Month == dhMonth);
        }

        var deliveries = await query
            .OrderByDescending(d => d.DeliveredAt)
            .ThenByDescending(d => d.CreatedAt)
            .ToListAsync();

        var allRows = deliveries
            .GroupBy(d => (d.ClientId, d.DeliveredAt, d.IsCarryOver))
            .Select(g => new DeliveryHistoryRow
            {
                ClientId = g.Key.ClientId,
                ClientName = g.First().Client.ClientName,
                DeliveredAt = g.Key.DeliveredAt,
                IsCarryOver = g.Key.IsCarryOver,
                ProductCount = g.Count(),
                TotalQuantity = g.Sum(d => d.Quantity)
            })
            .OrderByDescending(r => r.DeliveredAt)
            .ThenBy(r => r.ClientName)
            .ToList();

        vm.TotalCount = allRows.Count;
        vm.Rows = allRows.Skip(page * DeliveryHistoryViewModel.PageSize).Take(DeliveryHistoryViewModel.PageSize).ToList();

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int clientId, string deliveredAt, bool isCarryOver, string? returnYearMonth)
    {
        if (!DateOnly.TryParseExact(deliveredAt, "yyyy-MM-dd",
                null, System.Globalization.DateTimeStyles.None, out var date))
            return BadRequest();

        var toDelete = await db.Deliveries
            .Where(d => d.ClientId == clientId
                        && d.DeliveredAt == date
                        && d.IsCarryOver == isCarryOver)
            .ToListAsync();

        db.Deliveries.RemoveRange(toDelete);
        await db.SaveChangesAsync();
        TempData["Success"] = "納品記録を削除しました。";

        return RedirectToAction(nameof(Index), new { clientId, yearMonth = returnYearMonth });
    }
}
