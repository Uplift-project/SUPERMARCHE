using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Products;

[HasPermission(Permissions.ProductsView)]
public class DetailsModel(AppDbContext db, SaleService sales) : AppPageModel
{
    public Product Product { get; set; } = null!;
    public decimal Discount { get; set; }
    public List<StockMovement> Movements { get; set; } = [];
    public List<AuditLog> History { get; set; } = [];
    public int SoldLast30 { get; set; }
    public decimal RevenueLast30 { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var p = await db.Products.AsNoTracking().Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        Product = p;

        Discount = (await sales.GetActiveDiscountsAsync()).GetValueOrDefault(id);

        if (Can(Permissions.StockView))
            Movements = await db.StockMovements.AsNoTracking().Include(m => m.User)
                .Where(m => m.ProductId == id).OrderByDescending(m => m.Date).Take(20).ToListAsync();

        if (Can(Permissions.AuditView))
            History = await db.AuditLogs.AsNoTracking()
                .Where(a => a.EntityType == "Produit" && a.EntityId == id.ToString())
                .OrderByDescending(a => a.Id).Take(20).ToListAsync();

        var from = DateTime.Today.AddDays(-29);
        var items = await db.SaleItems.AsNoTracking().Where(i => i.ProductId == id && i.Sale!.Date >= from)
            .Select(i => new { i.Quantity, i.LineTotal }).ToListAsync();
        SoldLast30 = items.Sum(i => i.Quantity);
        RevenueLast30 = items.Sum(i => i.LineTotal);

        return Page();
    }
}
