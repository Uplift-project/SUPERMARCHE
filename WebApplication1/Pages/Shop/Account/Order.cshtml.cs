using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Shop.Account;

public class OrderModel(AppDbContext db, OrderService orders) : ShopPageModel
{
    public Order Order { get; set; } = null!;

    // Les URL sont en minuscules : les numéros sont stockés en majuscules (CMD…).
    public override void OnPageHandlerExecuting(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context)
    {
        if (context.HandlerArguments.TryGetValue("number", out var n) && n is string s)
            context.HandlerArguments["number"] = s.ToUpperInvariant();
    }

    public async Task<IActionResult> OnGetAsync(string number)
    {
        var id = await Shop.GetCustomerIdAsync();
        if (id == null) return RedirectToPage("/Shop/Account/Login", new { returnUrl = $"/compte/commandes/{number}" });

        var o = await db.Orders.AsNoTracking().Include(x => x.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.Number == number && x.CustomerId == id);
        if (o == null) return NotFound();
        Order = o;
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(string number)
    {
        var id = await Shop.GetCustomerIdAsync();
        if (id == null) return RedirectToPage("/Shop/Account/Login");
        var o = await db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Number == number && x.CustomerId == id);
        if (o == null) return NotFound();

        var (ok, message) = await orders.CancelByCustomerAsync(o.Id, id.Value);
        Flash(ok ? "Votre commande a été annulée." : message, ok ? "success" : "error");
        return RedirectToPage(new { number });
    }

    public async Task<IActionResult> OnPostReorderAsync(string number)
    {
        var id = await Shop.GetCustomerIdAsync();
        if (id == null) return RedirectToPage("/Shop/Account/Login");
        var items = await db.OrderItems.AsNoTracking()
            .Where(i => i.Order!.Number == number && i.Order.CustomerId == id && i.Product!.IsActive && i.Product.Quantity > 0)
            .Select(i => new { i.ProductId, i.Quantity, Stock = i.Product!.Quantity }).ToListAsync();
        foreach (var i in items) Shop.Set(i.ProductId, Math.Min(Shop.QuantityOf(i.ProductId) + i.Quantity, i.Stock));
        Flash(items.Count > 0 ? $"{items.Count} produit(s) ajouté(s) à votre panier." : "Ces produits ne sont plus disponibles.", items.Count > 0 ? "success" : "error");
        return RedirectToPage("/Shop/Cart");
    }
}
