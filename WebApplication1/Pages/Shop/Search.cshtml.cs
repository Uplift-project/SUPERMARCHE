using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Shop;

public class SearchModel(AppDbContext db, CatalogService catalog) : ShopPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    public List<ProductCardVm> Results { get; set; } = [];
    public List<CategoryNode> MatchingCategories { get; set; } = [];

    public async Task OnGetAsync()
    {
        var q = Q?.Trim();
        if (string.IsNullOrEmpty(q)) return;

        // Recherche insensible aux accents : on compare les « slugs » (tomates == Tomates, legumes == Légumes)
        var needle = Models.Slug.From(q);
        var words = needle.Split('-', StringSplitOptions.RemoveEmptyEntries);

        var flat = await catalog.GetFlatAsync();
        MatchingCategories = flat.Where(n => words.All(w => n.Slug.Contains(w))).Take(6).ToList();
        var catIds = MatchingCategories.SelectMany(n => n.SelfAndDescendants()).Select(n => n.Id).ToHashSet();

        var all = await db.Products.AsNoTracking().Include(p => p.Category).Where(p => p.IsActive).ToListAsync();
        var hits = all.Select(p => new { p, s = Models.Slug.From(p.Name + " " + p.Description) })
            .Where(x => words.All(w => x.s.Contains(w)) || catIds.Contains(x.p.CategoryId) || x.p.Barcode == q)
            .OrderByDescending(x => Models.Slug.From(x.p.Name).StartsWith(needle))
            .ThenBy(x => x.p.Quantity <= 0).ThenBy(x => x.p.Name)
            .Select(x => x.p).Take(60);
        Results = await hits.ToCardsAsync(catalog);
    }
}
