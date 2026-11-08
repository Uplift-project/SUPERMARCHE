using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Admin.Users;

[HasPermission(Permissions.UsersManage)]
public class IndexModel(AppDbContext db) : AppPageModel
{
    public record Row(AppUser User, int Actions30);

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int? RoleId { get; set; }

    public List<Row> Rows { get; set; } = [];
    public List<Role> Roles { get; set; } = [];

    public async Task OnGetAsync()
    {
        Roles = await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync();

        var q = db.Users.AsNoTracking().Include(u => u.Role).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q)) q = q.Where(u => u.FullName.Contains(Q) || u.Email.Contains(Q));
        if (RoleId is > 0) q = q.Where(u => u.RoleId == RoleId);
        var users = await q.OrderByDescending(u => u.IsActive).ThenBy(u => u.FullName).ToListAsync();

        var since = DateTime.Now.AddDays(-30);
        var counts = await db.AuditLogs.Where(a => a.Timestamp >= since && a.UserId != null)
            .GroupBy(a => a.UserId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        Rows = users.Select(u => new Row(u, counts.GetValueOrDefault(u.Id))).ToList();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        if (user.Id == Access.User!.Id)
        {
            Error("Vous ne pouvez pas désactiver votre propre compte.");
            return RedirectToPage();
        }
        if (user.Role!.IsSuperAdmin && !Access.IsSuperAdmin)
        {
            Error("Seul un Super Admin peut modifier un compte Super Admin.");
            return RedirectToPage();
        }

        user.IsActive = !user.IsActive;
        await db.SaveChangesAsync();
        Success($"Compte de {user.FullName} {(user.IsActive ? "réactivé" : "désactivé (il est déconnecté immédiatement)")}.");
        return RedirectToPage();
    }
}
