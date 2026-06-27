using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaiko.Data;
using Zaiko.Models;
using Zaiko.ViewModels;

namespace Zaiko.Controllers;

[Authorize]
public class ClientController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> CheckDuplicate(string name, int? currentId = null)
    {
        var trimmed = (name ?? "").Trim();
        bool exists = await db.Clients.AnyAsync(c => c.ClientName == trimmed && (currentId == null || c.ClientId != currentId));
        return Json(new { exists });
    }

    public async Task<IActionResult> Index()
    {
        var clients = await db.Clients
            .OrderBy(c => c.ClientId)
            .Select(c => new ClientListItemViewModel
            {
                ClientId = c.ClientId,
                ClientName = c.ClientName,
                FaxNumber = c.FaxNumber,
                IsActive = c.IsActive,
                ClientProductCount = c.ClientProducts.Count()
            })
            .ToListAsync();

        return View(clients);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        var vm = new ClientEditViewModel();
        var checkedProductIds = new HashSet<int>();
        var existingRates = new Dictionary<int, decimal>();
        var existingSortOrders = new Dictionary<int, int>();

        if (id.HasValue)
        {
            var client = await db.Clients
                .Include(c => c.ClientProducts)
                .FirstOrDefaultAsync(c => c.ClientId == id.Value);
            if (client == null) return NotFound();

            vm.ClientId = client.ClientId;
            vm.ClientName = client.ClientName;
            vm.FaxNumber = client.FaxNumber;
            vm.IsActive = client.IsActive;
            checkedProductIds = client.ClientProducts.Select(cp => cp.ProductId).ToHashSet();
            existingRates = client.ClientProducts.ToDictionary(cp => cp.ProductId, cp => cp.CommissionRate);
            existingSortOrders = client.ClientProducts.ToDictionary(cp => cp.ProductId, cp => cp.SortOrder);
        }

        vm.Products = await BuildProductRows(checkedProductIds, existingRates, existingSortOrders);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ClientEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await ReloadDisplayInfo(vm.Products);
            return View(vm);
        }

        if (vm.ClientId.HasValue)
        {
            var client = await db.Clients
                .Include(c => c.ClientProducts)
                .FirstOrDefaultAsync(c => c.ClientId == vm.ClientId.Value);
            if (client == null) return NotFound();

            client.ClientName = vm.ClientName;
            client.FaxNumber = string.IsNullOrWhiteSpace(vm.FaxNumber) ? null : vm.FaxNumber;
            await UpsertClientProducts(client.ClientProducts.ToList(), client.ClientId, vm.Products);
        }
        else
        {
            var client = new Client
            {
                ClientName = vm.ClientName,
                FaxNumber = string.IsNullOrWhiteSpace(vm.FaxNumber) ? null : vm.FaxNumber,
                IsActive = true
            };
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            await UpsertClientProducts([], client.ClientId, vm.Products);
        }

        await db.SaveChangesAsync();
        TempData["Success"] = "取引先を保存しました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var client = await db.Clients
            .Include(c => c.Deliveries)
            .Include(c => c.SalesReports)
            .Include(c => c.ClientProducts)
            .FirstOrDefaultAsync(c => c.ClientId == id);
        if (client == null) return NotFound();

        bool hasRelated = client.Deliveries.Any() || client.SalesReports.Any() || client.ClientProducts.Any();
        if (hasRelated)
        {
            client.IsActive = false;
            await db.SaveChangesAsync();
            TempData["Success"] = "関連データがあるため、取引先を無効化しました。";
        }
        else
        {
            db.Clients.Remove(client);
            await db.SaveChangesAsync();
            TempData["Success"] = "取引先を削除しました。";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var client = await db.Clients.FindAsync(id);
        if (client == null) return NotFound();

        client.IsActive = !client.IsActive;
        await db.SaveChangesAsync();
        TempData["Success"] = client.IsActive ? "取引先を有効化しました。" : "取引先を無効化しました。";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<ClientProductRowViewModel>> BuildProductRows(
        HashSet<int> checkedIds, Dictionary<int, decimal> existingRates, Dictionary<int, int> existingSortOrders)
    {
        var products = await db.Products
            .Include(p => p.Color)
            .OrderBy(p => p.ProductName)
            .ThenBy(p => p.ColorId)
            .ToListAsync();

        return products
            .Select(p => new ClientProductRowViewModel
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                ColorName = p.Color?.ColorName,
                RetailPrice = p.RetailPrice,
                IsChecked = checkedIds.Contains(p.ProductId),
                CommissionRate = existingRates.TryGetValue(p.ProductId, out var r) ? r : p.CommissionRate,
                SortOrder = existingSortOrders.TryGetValue(p.ProductId, out var so) ? so : int.MaxValue
            })
            .OrderBy(r => r.IsChecked ? 0 : 1)
            .ThenBy(r => r.SortOrder)
            .ThenBy(r => r.ProductName)
            .ThenBy(r => r.ColorName)
            .ToList();
    }

    private async Task ReloadDisplayInfo(List<ClientProductRowViewModel> rows)
    {
        var products = await db.Products.Include(p => p.Color)
            .ToDictionaryAsync(p => p.ProductId);
        foreach (var row in rows)
        {
            if (products.TryGetValue(row.ProductId, out var p))
            {
                row.ProductName = p.ProductName;
                row.ColorName = p.Color?.ColorName;
                row.RetailPrice = p.RetailPrice;
            }
        }
    }

    private async Task UpsertClientProducts(
        List<ClientProduct> existing, int clientId, List<ClientProductRowViewModel> rows)
    {
        var checkedRows = rows.Where(r => r.IsChecked).ToList();

        var toDelete = existing.Where(e => !checkedRows.Any(r => r.ProductId == e.ProductId));
        db.ClientProducts.RemoveRange(toDelete);

        foreach (var row in checkedRows)
        {
            var cp = existing.FirstOrDefault(e => e.ProductId == row.ProductId);
            if (cp != null)
            {
                cp.CommissionRate = row.CommissionRate;
                cp.SortOrder = row.SortOrder;
            }
            else
            {
                db.ClientProducts.Add(new ClientProduct
                {
                    ClientId = clientId,
                    ProductId = row.ProductId,
                    CommissionRate = row.CommissionRate,
                    SortOrder = row.SortOrder
                });
            }
        }
    }
}
