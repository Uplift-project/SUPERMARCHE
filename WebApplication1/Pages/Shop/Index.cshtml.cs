using Microsoft.EntityFrameworkCore;
using Models;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Shop;

public class IndexModel(AppDbContext db, CatalogService catalog) : ShopPageModel
{
    public record Rail(CategoryNode Root, List<ProductCardVm> Products, int Total);

    public List<CategoryNode> Roots { get; set; } = [];
    public List<ProductCardVm> Deals { get; set; } = [];
    public List<ProductCardVm> BestSellers { get; set; } = [];
    public List<Rail> Rails { get; set; } = [];
    public List<Promotion> RunningPromos { get; set; } = [];

    public async Task OnGetAsync()
    {
        Roots = await catalog.GetTreeAsync();
        var discounts = await catalog.GetActiveDiscountsAsync();

        var active = db.Products.AsNoTracking().Include(p => p.Category).Where(p => p.IsActive);

        var dealIds = discounts.OrderByDescending(d => d.Value).Select(d => d.Key).Take(12).ToList();
        Deals = await (await active.Where(p => dealIds.Contains(p.Id)).ToListAsync())
            .OrderByDescending(p => discounts.GetValueOrDefault(p.Id)).ThenBy(p => p.Quantity <= 0).ToCardsAsync(catalog);

        // Meilleures ventes (magasin + boutique) des 30 derniers jours
        var since = DateTime.Today.AddDays(-30);
        var sold = await db.SaleItems.AsNoTracking().Where(i => i.Sale!.Date >= since)
            .GroupBy(i => i.ProductId).Select(g => new { g.Key, Qty = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Qty).Take(10).Select(x => x.Key).ToListAsync();
        BestSellers = await (await active.Where(p => sold.Contains(p.Id) && p.Quantity > 0).ToListAsync())
            .OrderBy(p => sold.IndexOf(p.Id)).ToCardsAsync(catalog);

        foreach (var root in Roots)
        {
            var ids = root.SelfAndDescendants().Select(n => n.Id).ToList();
            var q = active.Where(p => ids.Contains(p.CategoryId));
            var list = await q.OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.Quantity > 0).ThenBy(p => p.Name).Take(10).ToListAsync();
            if (list.Count > 0) Rails.Add(new Rail(root, await list.ToCardsAsync(catalog), await q.CountAsync()));
        }

        var today = DateTime.Today;
        RunningPromos = await db.Promotions.AsNoTracking().Include(p => p.Category).Include(p => p.Product)
            .Where(p => p.IsActive && p.StartDate <= today && p.EndDate >= today)
            .OrderByDescending(p => p.DiscountPercent).Take(3).ToListAsync();
    }
}
