using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyPenny.Models;

namespace MoneyPenny.Controllers;

[Authorize(Roles = "Admin")]
public class UserManagementController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UserManagementController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> Index(string search, string roleFilter, string statusFilter)
    {
        var users = await _userManager.Users.ToListAsync();
        var userViewModels = new List<UserManagementViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userViewModels.Add(new UserManagementViewModel
            {
                User = user,
                Roles = roles.ToList()
            });
        }

        // Aplicar búsqueda
        if (!string.IsNullOrWhiteSpace(search))
        {
            userViewModels = userViewModels.Where(u => 
                (u.User.Email != null && u.User.Email.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (u.User.DisplayName != null && u.User.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
            ).ToList();
            ViewData["SearchQuery"] = search;
        }

        // Aplicar filtro de rol
        if (!string.IsNullOrWhiteSpace(roleFilter))
        {
            userViewModels = userViewModels.Where(u => u.Roles.Contains(roleFilter)).ToList();
        }

        // Aplicar filtro de estado
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            userViewModels = statusFilter switch
            {
                "active" => userViewModels.Where(u => u.User.LastLoginAt != null && u.User.LastLoginAt >= thirtyDaysAgo).ToList(),
                "inactive" => userViewModels.Where(u => u.User.LastLoginAt == null || u.User.LastLoginAt < thirtyDaysAgo).ToList(),
                _ => userViewModels
            };
        }

        return View(userViewModels);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                DisplayName = model.DisplayName,
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var userRoles = await _userManager.GetRolesAsync(user);
        var allRoles = await _roleManager.Roles.ToListAsync();

        var model = new EditUserViewModel
        {
            Id = user.Id,
            Email = user.Email!,
            DisplayName = user.DisplayName ?? "",
            UserRoles = userRoles.ToList(),
            AllRoles = allRoles.Select(r => r.Name!).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return NotFound();
            }

            user.DisplayName = model.DisplayName;
            user.Email = model.Email;
            user.UserName = model.Email;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                var rolesToAdd = model.SelectedRoles.Except(currentRoles);
                var rolesToRemove = currentRoles.Except(model.SelectedRoles);

                await _userManager.AddToRolesAsync(user, rolesToAdd);
                await _userManager.RemoveFromRolesAsync(user, rolesToRemove);

                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        var allRoles = await _roleManager.Roles.ToListAsync();
        model.AllRoles = allRoles.Select(r => r.Name!).ToList();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.Id == user.Id)
        {
            TempData["Error"] = "No puedes eliminar tu propia cuenta.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] = "Usuario eliminado correctamente.";
        }
        else
        {
            TempData["Error"] = "Error al eliminar el usuario.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Roles()
    {
        var roles = await _roleManager.Roles.ToListAsync();
        return View(roles);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRole(string roleName)
    {
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            var roleExists = await _roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
                TempData["Success"] = $"Rol '{roleName}' creado correctamente.";
            }
            else
            {
                TempData["Error"] = $"El rol '{roleName}' ya existe.";
            }
        }

        return RedirectToAction(nameof(Roles));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRole(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role != null && role.Name != "Admin" && role.Name != "User")
        {
            await _roleManager.DeleteAsync(role);
            TempData["Success"] = "Rol eliminado correctamente.";
        }
        else
        {
            TempData["Error"] = "No se puede eliminar este rol.";
        }

        return RedirectToAction(nameof(Roles));
    }
}
