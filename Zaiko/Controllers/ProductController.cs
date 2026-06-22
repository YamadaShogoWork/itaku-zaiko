using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaiko.Data;
using Zaiko.Models;
using Zaiko.ViewModels;

namespace Zaiko.Controllers;

[Authorize]
public class ProductController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var products = await db.Products
            .Include(p => p.Color)
            .OrderBy(p => p.ProductId)
            .ThenBy(p => p.ColorId)
            .ToListAsync();

        var deliveryIds = await db.Deliveries.Select(d => d.ProductId).Distinct().ToHashSetAsync();
        var salesIds = await db.SalesReports.Select(sr => sr.ProductId).Distinct().ToHashSetAsync();
        var cpIds = await db.ClientProducts.Select(cp => cp.ProductId).Distinct().ToHashSetAsync();
        var hasRelated = deliveryIds.Union(salesIds).Union(cpIds).ToHashSet();

        var groups = products
            .GroupBy(p => p.ProductName)
            .OrderBy(g => g.Min(p => p.ProductId))
            .Select(g =>
            {
                var first = g.OrderBy(p => p.ProductId).First();
                return new ProductGroupViewModel
                {
                    ProductName = g.Key,
                    RetailPrice = first.RetailPrice,
                    CommissionRate = first.CommissionRate,
                    Colors = g.OrderBy(p => p.ColorId).Select(p => new ProductColorRow
                    {
                        ProductId = p.ProductId,
                        ColorId = p.ColorId,
                        ColorName = p.Color?.ColorName,
                        HasRelatedData = hasRelated.Contains(p.ProductId)
                    }).ToList()
                };
            })
            .ToList();

        return View(groups);
    }

    public async Task<IActionResult> Edit(string? productName)
    {
        var vm = new ProductEditViewModel();
        var existing = new List<Product>();

        if (productName != null)
        {
            existing = await db.Products
                .Include(p => p.Color)
                .Where(p => p.ProductName == productName)
                .OrderBy(p => p.ProductId)
                .ToListAsync();
            if (existing.Count == 0) return NotFound();

            var first = existing.First();
            vm.OriginalProductName = productName;
            vm.ProductName = productName;
            vm.RetailPrice = first.RetailPrice;
            vm.CommissionRate = first.CommissionRate;
            vm.OriginalCommissionRate = first.CommissionRate;

            bool hasColorVariants = existing.Any(p => p.ColorId.HasValue);
            vm.HasColorVariants = hasColorVariants;

            var existingColorIds = existing
                .Where(p => p.ColorId.HasValue)
                .Select(p => p.ColorId!.Value)
                .ToHashSet();
            vm.SelectedColorIds = [.. existingColorIds];

            var existingProductIds = existing.Select(p => p.ProductId).ToList();
            vm.HasClientProducts = await db.ClientProducts
                .AnyAsync(cp => existingProductIds.Contains(cp.ProductId));
        }

        vm.AllColors = await BuildColorSelectItems(
            vm.SelectedColorIds.ToHashSet(), existing);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await ReloadAllColors(vm);
            return View(vm);
        }

        var existing = vm.OriginalProductName != null
            ? await db.Products
                .Include(p => p.Color)
                .Where(p => p.ProductName == vm.OriginalProductName)
                .ToListAsync()
            : [];

        var existingProductIds = existing.Select(e => e.ProductId).ToList();

        // Validate uncheck of products with related data
        if (!vm.HasColorVariants)
        {
            var toRemove = existing.Where(p => p.ColorId.HasValue).ToList();
            var blocked = await GetBlockedColorNames(toRemove);
            if (blocked.Count > 0)
            {
                ModelState.AddModelError("", $"使用中の色があるため変更できません: {string.Join(", ", blocked)}");
                await ReloadAllColors(vm);
                return View(vm);
            }
            // Also check if ColorId=null product is being replaced and has related data
            var noColorExisting = existing.FirstOrDefault(p => p.ColorId == null);
            if (noColorExisting != null && await HasRelatedDataAsync(noColorExisting.ProductId))
            {
                // Keeping the no-color product, just updating it — not removing it, so this is fine
            }
        }
        else
        {
            if (!vm.SelectedColorIds.Any())
            {
                ModelState.AddModelError("SelectedColorIds", "色を1つ以上選択してください");
                await ReloadAllColors(vm);
                return View(vm);
            }

            var selectedSet = vm.SelectedColorIds.ToHashSet();
            var toRemove = existing
                .Where(p => p.ColorId.HasValue && !selectedSet.Contains(p.ColorId.Value))
                .ToList();
            var blocked = await GetBlockedColorNames(toRemove);
            if (blocked.Count > 0)
            {
                ModelState.AddModelError("", $"使用中の色があるためチェックを外せません: {string.Join(", ", blocked)}");
                await ReloadAllColors(vm);
                return View(vm);
            }

            var noColorExisting = existing.FirstOrDefault(p => p.ColorId == null);
            if (noColorExisting != null && await HasRelatedDataAsync(noColorExisting.ProductId))
            {
                ModelState.AddModelError("", "色なしの商品は使用中のため削除できません");
                await ReloadAllColors(vm);
                return View(vm);
            }
        }

        // Perform UPSERT / delete
        if (!vm.HasColorVariants)
        {
            var colorProducts = existing.Where(p => p.ColorId.HasValue).ToList();
            db.Products.RemoveRange(colorProducts);

            var noColor = existing.FirstOrDefault(p => p.ColorId == null);
            if (noColor != null)
            {
                noColor.ProductName = vm.ProductName;
                noColor.RetailPrice = vm.RetailPrice;
                noColor.CommissionRate = vm.CommissionRate;
            }
            else
            {
                db.Products.Add(new Product
                {
                    ProductName = vm.ProductName,
                    RetailPrice = vm.RetailPrice,
                    CommissionRate = vm.CommissionRate
                });
            }
        }
        else
        {
            var noColor = existing.FirstOrDefault(p => p.ColorId == null);
            if (noColor != null) db.Products.Remove(noColor);

            var selectedSet = vm.SelectedColorIds.ToHashSet();
            var toDelete = existing
                .Where(p => p.ColorId.HasValue && !selectedSet.Contains(p.ColorId.Value))
                .ToList();
            db.Products.RemoveRange(toDelete);

            foreach (var colorId in vm.SelectedColorIds)
            {
                var existingProduct = existing.FirstOrDefault(p => p.ColorId == colorId);
                if (existingProduct != null)
                {
                    existingProduct.ProductName = vm.ProductName;
                    existingProduct.RetailPrice = vm.RetailPrice;
                    existingProduct.CommissionRate = vm.CommissionRate;
                }
                else
                {
                    db.Products.Add(new Product
                    {
                        ProductName = vm.ProductName,
                        RetailPrice = vm.RetailPrice,
                        CommissionRate = vm.CommissionRate,
                        ColorId = colorId
                    });
                }
            }
        }

        if (vm.UpdateClientProducts && existingProductIds.Count > 0)
        {
            var clientProducts = await db.ClientProducts
                .Where(cp => existingProductIds.Contains(cp.ProductId))
                .ToListAsync();
            foreach (var cp in clientProducts)
                cp.CommissionRate = vm.CommissionRate;
        }

        await db.SaveChangesAsync();
        TempData["Success"] = "商品を保存しました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await db.Products.FindAsync(id);
        if (product == null) return NotFound();

        if (await HasRelatedDataAsync(id))
        {
            TempData["Error"] = "この商品は使用されているため削除できません。";
            return RedirectToAction(nameof(Index));
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync();
        TempData["Success"] = "商品を削除しました。";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> HasRelatedDataAsync(int productId) =>
        await db.Deliveries.AnyAsync(d => d.ProductId == productId)
        || await db.SalesReports.AnyAsync(sr => sr.ProductId == productId)
        || await db.ClientProducts.AnyAsync(cp => cp.ProductId == productId);

    private async Task<List<string>> GetBlockedColorNames(List<Product> products)
    {
        var blocked = new List<string>();
        foreach (var p in products)
        {
            if (await HasRelatedDataAsync(p.ProductId))
                blocked.Add(p.Color?.ColorName ?? $"ProductId:{p.ProductId}");
        }
        return blocked;
    }

    private async Task<List<ColorSelectItem>> BuildColorSelectItems(
        HashSet<int> selectedIds, List<Product> existing)
    {
        var colors = await db.Colors.OrderBy(c => c.ColorId).ToListAsync();
        var hasRelated = new HashSet<int>();
        foreach (var p in existing.Where(p => p.ColorId.HasValue))
        {
            if (await HasRelatedDataAsync(p.ProductId))
                hasRelated.Add(p.ColorId!.Value);
        }

        return colors.Select(c => new ColorSelectItem
        {
            ColorId = c.ColorId,
            ColorName = c.ColorName,
            IsChecked = selectedIds.Contains(c.ColorId),
            HasRelatedData = hasRelated.Contains(c.ColorId)
        }).ToList();
    }

    private async Task ReloadAllColors(ProductEditViewModel vm)
    {
        var selectedSet = vm.SelectedColorIds?.ToHashSet() ?? [];
        vm.AllColors = await BuildColorSelectItems(selectedSet, []);
    }
}
