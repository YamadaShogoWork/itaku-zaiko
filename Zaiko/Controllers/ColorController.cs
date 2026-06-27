using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaiko.Data;
using Zaiko.Models;
using Zaiko.ViewModels;

namespace Zaiko.Controllers;

[Authorize]
public class ColorController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> CheckDuplicate(string name, int? currentId = null)
    {
        var trimmed = (name ?? "").Trim();
        bool exists = await db.Colors.AnyAsync(c => c.ColorName == trimmed && (currentId == null || c.ColorId != currentId));
        return Json(new { exists });
    }

    public async Task<IActionResult> Index()
    {
        var colors = await db.Colors
            .OrderBy(c => c.ColorId)
            .Select(c => new ColorIndexItemViewModel
            {
                ColorId = c.ColorId,
                ColorName = c.ColorName,
                ProductCount = c.Products.Count()
            })
            .ToListAsync();

        return View(colors);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string colorName)
    {
        if (string.IsNullOrWhiteSpace(colorName))
        {
            TempData["Error"] = "色名は必須です。";
            return RedirectToAction(nameof(Index));
        }

        var trimmed = colorName.Trim();
        bool exists = await db.Colors.AnyAsync(c => c.ColorName == trimmed);
        if (exists)
        {
            TempData["Error"] = $"色名「{trimmed}」は既に登録されています。";
            return RedirectToAction(nameof(Index));
        }

        db.Colors.Add(new Color { ColorName = trimmed });
        await db.SaveChangesAsync();
        TempData["Success"] = "色を追加しました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string colorName)
    {
        if (string.IsNullOrWhiteSpace(colorName))
        {
            TempData["Error"] = "色名は必須です。";
            return RedirectToAction(nameof(Index));
        }

        var color = await db.Colors.FindAsync(id);
        if (color == null) return NotFound();

        var trimmed = colorName.Trim();
        bool exists = await db.Colors.AnyAsync(c => c.ColorName == trimmed && c.ColorId != id);
        if (exists)
        {
            TempData["Error"] = $"色名「{trimmed}」は既に登録されています。";
            return RedirectToAction(nameof(Index));
        }

        color.ColorName = trimmed;
        await db.SaveChangesAsync();
        TempData["Success"] = "色名を更新しました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var color = await db.Colors
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.ColorId == id);
        if (color == null) return NotFound();

        if (color.Products.Any())
        {
            TempData["Error"] = "この色は商品で使用されているため削除できません。";
            return RedirectToAction(nameof(Index));
        }

        db.Colors.Remove(color);
        await db.SaveChangesAsync();
        TempData["Success"] = "色を削除しました。";
        return RedirectToAction(nameof(Index));
    }
}
