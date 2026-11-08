using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Shop;

public class ProductModel(AppDbContext db, CatalogService catalog) : ShopPageModel
{
    public ProductCardVm Card { get; set; } = null!;
    public CategoryNode Node { get; set; } = null!;
    public List<ProductCardVm> Similar { get; set; } = [];
    public int InCart { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, string? slug)
    {
        var p = await db.Products.AsNoTracking().Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (p == null) return NotFound();

        // URL canonique avec le nom du produit
        var expected = Models.Slug.From(p.Name);
        if (slug != expected) return RedirectToPagePermanent("/Shop/Product", new { id, slug = expected });

        Card = (await new[] { p }.ToCardsAsync(catalog))[0];
        Node = (await catalog.FindAsync(p.CategoryId))!;
        ViewData["ActiveRoot"] = Node.Root.Id;
        InCart = Shop.QuantityOf(p.Id);

        var siblings = Node.Parent?.SelfAndDescendants().Select(n => n.Id).ToList() ?? [Node.Id];
        var similar = await db.Products.AsNoTracking().Include(x => x.Category)
            .Where(x => x.IsActive && x.Id != p.Id && siblings.Contains(x.CategoryId) && x.Quantity > 0)
            .ToListAsync();
        Similar = await similar.OrderByDescending(x => x.CategoryId == p.CategoryId).ThenBy(_ => Random.Shared.Next())
            .Take(10).ToCardsAsync(catalog);
        return Page();
    }
}
