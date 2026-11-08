using Microsoft.EntityFrameworkCore;
using Models;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Shop;

public class PromotionsModel(AppDbContext db, CatalogService catalog) : ShopPageModel
{
    public record Group(CategoryNode Root, List<ProductCardVm> Products);

    public List<Promotion> Running { get; set; } = [];
    public List<Group> Groups { get; set; } = [];

    public async Task OnGetAsync()
    {
        var today = DateTime.Today;
        Running = await db.Promotions.AsNoTracking().Include(p => p.Category).Include(p => p.Product)
            .Where(p => p.IsActive && p.StartDate <= today && p.EndDate >= today)
            .OrderByDescending(p => p.DiscountPercent).ToListAsync();

        var discounts = await catalog.GetActiveDiscountsAsync();
        var ids = discounts.Keys.ToList();
        var products = await db.Products.AsNoTracking().Include(p => p.Category)
            .Where(p => p.IsActive && ids.Contains(p.Id)).ToListAsync();
        var cards = await products.OrderByDescending(p => discounts[p.Id]).ThenBy(p => p.Name).ToCardsAsync(catalog);

        foreach (var root in await catalog.GetTreeAsync())
        {
            var inRoot = root.SelfAndDescendants().Select(n => n.Id).ToHashSet();
            var list = cards.Where(c => inRoot.Contains(c.Product.CategoryId)).ToList();
            if (list.Count > 0) Groups.Add(new Group(root, list));
        }
    }
}
