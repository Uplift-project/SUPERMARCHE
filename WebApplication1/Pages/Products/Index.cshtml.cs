using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Models;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Products;

[HasPermission(Permissions.ProductsView)]
public class IndexModel(AppDbContext db, SaleService sales, CatalogService catalog) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Stock { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedList<Product> Products { get; set; } = new();
    public List<SelectListItem> Categories { get; set; } = [];
    public Dictionary<int, decimal> Discounts { get; set; } = [];

    public async Task OnGetAsync()
    {
        Categories = await catalog.CategoryOptionsAsync(CategoryId);
        Discounts = await sales.GetActiveDiscountsAsync();

        var q = db.Products.AsNoTracking().Include(p => p.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
            q = q.Where(p => p.Name.Contains(Q) || (p.Barcode != null && p.Barcode.Contains(Q)));
        if (CategoryId is > 0)
        {
            // Un rayon inclut toutes ses sous-catégories
            var ids = await catalog.SelfAndDescendantIdsAsync(CategoryId.Value);
            q = q.Where(p => ids.Contains(p.CategoryId));
        }
        q = Stock switch
        {
            "low" => q.Where(p => p.Quantity <= p.MinQuantity && p.Quantity > 0),
            "out" => q.Where(p => p.Quantity == 0),
            _ => q,
        };
        q = Status switch
        {
            "inactive" => q.Where(p => !p.IsActive),
            "all" => q,
            _ => q.Where(p => p.IsActive),
        };

        Products = await q.OrderBy(p => p.Name).ToPagedAsync(PageNumber);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (Require(Permissions.ProductsDelete) is { } denied) return denied;

        var product = await db.Products.FindAsync(id);
        if (product == null) return NotFound();

        if (await db.SaleItems.AnyAsync(i => i.ProductId == id) || await db.OrderItems.AnyAsync(i => i.ProductId == id))
        {
            Error($"« {product.Name} » apparaît dans des ventes ou des commandes : il ne peut pas être supprimé. Désactivez-le plutôt.");
            return RedirectToPage();
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync();
        Success($"Produit « {product.Name} » supprimé.");
        return RedirectToPage();
    }
}
