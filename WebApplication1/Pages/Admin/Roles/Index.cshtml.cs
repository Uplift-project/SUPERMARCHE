using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Admin.Roles;

[HasPermission(Permissions.RolesManage)]
public class IndexModel(AppDbContext db) : AppPageModel
{
    public List<(Role Role, int Users)> Roles { get; set; } = [];

    public async Task OnGetAsync()
    {
        var roles = await db.Roles.AsNoTracking().Include(r => r.Users).OrderByDescending(r => r.IsSuperAdmin).ThenBy(r => r.Name).ToListAsync();
        Roles = roles.Select(r => (r, r.Users.Count)).ToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var role = await db.Roles.FindAsync(id);
        if (role == null) return NotFound();
        if (role.IsSuperAdmin)
        {
            Error("Le rôle Super Admin ne peut pas être supprimé.");
            return RedirectToPage();
        }
        if (await db.Users.AnyAsync(u => u.RoleId == id))
        {
            Error($"Le rôle « {role.Name} » est attribué à des employés : changez d'abord leur rôle.");
            return RedirectToPage();
        }
        db.Roles.Remove(role);
        await db.SaveChangesAsync();
        Success($"Rôle « {role.Name} » supprimé.");
        return RedirectToPage();
    }
}
