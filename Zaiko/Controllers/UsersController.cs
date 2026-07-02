using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Zaiko.Models;
using Zaiko.ViewModels;

namespace Zaiko.Controllers;

[Authorize]
public class UsersController(UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> CheckDuplicate(string name, string? currentId = null)
    {
        var trimmed = (name ?? "").Trim();
        var existing = await userManager.FindByNameAsync(trimmed);
        bool exists = existing != null && existing.Id != currentId;
        return Json(new { exists });
    }

    public async Task<IActionResult> Index()
    {
        var currentUser = await userManager.GetUserAsync(User);
        var allUsers = userManager.Users.OrderBy(u => u.CreatedAt).ToList();
        bool isLast = allUsers.Count == 1;

        var vm = allUsers.Select(u => new UserListItemViewModel
        {
            Id = u.Id,
            UserName = u.UserName ?? string.Empty,
            CreatedAt = u.CreatedAt,
            IsSelf = u.Id == currentUser?.Id,
            CanDelete = u.Id != currentUser?.Id && !isLast
        }).ToList();

        return View(vm);
    }

    public async Task<IActionResult> Edit(string? id)
    {
        if (id == null)
            return View(new UserEditViewModel());

        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        return View(new UserEditViewModel
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditViewModel vm)
    {
        bool isNew = vm.UserId == null;

        if (isNew && string.IsNullOrEmpty(vm.Password))
            ModelState.AddModelError(nameof(vm.Password), "新規登録時はパスワードは必須です");

        if (!ModelState.IsValid)
            return View(vm);

        if (isNew)
        {
            var existing = await userManager.FindByNameAsync(vm.UserName);
            if (existing != null)
            {
                ModelState.AddModelError(nameof(vm.UserName), "このユーザー名は既に使用されています");
                return View(vm);
            }

            var user = new ApplicationUser
            {
                UserName = vm.UserName,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(user, vm.Password!);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);
                return View(vm);
            }
        }
        else
        {
            var user = await userManager.FindByIdAsync(vm.UserId!);
            if (user == null) return NotFound();

            if (user.UserName != vm.UserName)
            {
                var existing = await userManager.FindByNameAsync(vm.UserName);
                if (existing != null && existing.Id != user.Id)
                {
                    ModelState.AddModelError(nameof(vm.UserName), "このユーザー名は既に使用されています");
                    return View(vm);
                }

                var setNameResult = await userManager.SetUserNameAsync(user, vm.UserName);
                if (!setNameResult.Succeeded)
                {
                    foreach (var err in setNameResult.Errors)
                        ModelState.AddModelError(string.Empty, err.Description);
                    return View(vm);
                }
            }

            if (!string.IsNullOrEmpty(vm.Password))
            {
                await userManager.RemovePasswordAsync(user);
                var setPwResult = await userManager.AddPasswordAsync(user, vm.Password);
                if (!setPwResult.Succeeded)
                {
                    foreach (var err in setPwResult.Errors)
                        ModelState.AddModelError(string.Empty, err.Description);
                    return View(vm);
                }
            }
        }

        TempData["Success"] = isNew ? "ユーザーを登録しました。" : "ユーザー情報を更新しました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser?.Id == id)
        {
            TempData["Error"] = "自分自身は削除できません。";
            return RedirectToAction(nameof(Index));
        }

        var allCount = userManager.Users.Count();
        if (allCount <= 1)
        {
            TempData["Error"] = "最後のユーザーは削除できません。";
            return RedirectToAction(nameof(Index));
        }

        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        await userManager.DeleteAsync(user);
        TempData["Success"] = "ユーザーを削除しました。";
        return RedirectToAction(nameof(Index));
    }
}
