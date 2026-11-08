using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Shop;

public class CategoryModel(AppDbContext db, CatalogService catalog) : ShopPageModel
{
    public const int PageSize = 24;

    [BindProperty(SupportsGet = true)] public string Slug { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true)] public bool Promo { get; set; }
    [BindProperty(SupportsGet = true)] public bool InStock { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public CategoryNode Node { get; set; } = null!;
    public List<ProductCardVm> Products { get; set; } = [];
    public int Total { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    public Dictionary<int, int> Counts { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var node = await catalog.FindBySlugAsync(Slug);
        if (node == null) return NotFound();
        Node = node;
        ViewData["ActiveRoot"] = node.Root.Id;

        var ids = node.SelfAndDescendants().Select(n => n.Id).ToList();
        var products = await db.Products.AsNoTracking().Include(p => p.Category)
            .Where(p => p.IsActive && ids.Contains(p.CategoryId)).ToListAsync();

        // Nombre de produits par catégorie (sous-catégories incluses) pour la barre latérale
        var perCat = await db.Products.AsNoTracking().Where(p => p.IsActive)
            .GroupBy(p => p.CategoryId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        foreach (var n in node.Root.SelfAndDescendants())
            Counts[n.Id] = n.SelfAndDescendants().Sum(d => perCat.GetValueOrDefault(d.Id));

        var cards = await products.ToCardsAsync(catalog);
        if (Promo) cards = cards.Where(c => c.Discount > 0).ToList();
        if (InStock) cards = cards.Where(c => !c.OutOfStock).ToList();

        cards = Sort switch
        {
            "prix-asc" => cards.OrderBy(c => c.FinalPrice).ToList(),
            "prix-desc" => cards.OrderByDescending(c => c.FinalPrice).ToList(),
            "nom" => cards.OrderBy(c => c.Product.Name).ToList(),
            "promo" => cards.OrderByDescending(c => c.Discount).ThenBy(c => c.Product.Name).ToList(),
            "nouveau" => cards.OrderByDescending(c => c.Product.CreatedAt).ToList(),
            _ => cards.OrderBy(c => c.OutOfStock).ThenByDescending(c => c.Product.IsFeatured).ThenByDescending(c => c.Discount).ThenBy(c => c.Product.Name).ToList(),
        };

        Total = cards.Count;
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
        Products = cards.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToList();
        return Page();
    }

    public string PageUrl(int page) =>
        Url.Page("/Shop/Category", new { slug = Slug, sort = Sort, promo = Promo ? true : (bool?)null, inStock = InStock ? true : (bool?)null, p = page })!;
}
