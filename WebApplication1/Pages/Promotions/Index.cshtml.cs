using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Promotions;

[HasPermission(Permissions.PromotionsView)]
public class IndexModel(AppDbContext db) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    public List<Promotion> Promotions { get; set; } = [];

    public async Task OnGetAsync()
    {
        var today = DateTime.Today;
        var q = db.Promotions.AsNoTracking().Include(p => p.Product).Include(p => p.Category).AsQueryable();
        q = Status switch
        {
            "running" => q.Where(p => p.IsActive && p.StartDate <= today && p.EndDate >= today),
            "upcoming" => q.Where(p => p.IsActive && p.StartDate > today),
            "ended" => q.Where(p => p.EndDate < today || !p.IsActive),
            _ => q,
        };
        Promotions = await q.OrderByDescending(p => p.StartDate).ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        if (Require(Permissions.PromotionsManage) is { } denied) return denied;
        var promo = await db.Promotions.FindAsync(id);
        if (promo == null) return NotFound();
        promo.IsActive = !promo.IsActive;
        await db.SaveChangesAsync();
        Success($"Promotion « {promo.Name} » {(promo.IsActive ? "activée" : "désactivée")}.");
        return RedirectToPage(new { Status });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (Require(Permissions.PromotionsManage) is { } denied) return denied;
        var promo = await db.Promotions.FindAsync(id);
        if (promo == null) return NotFound();
        db.Promotions.Remove(promo);
        await db.SaveChangesAsync();
        Success($"Promotion « {promo.Name} » supprimée.");
        return RedirectToPage(new { Status });
    }
}
