using Microsoft.AspNetCore.Mvc;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Shop;

public class CartModel(AppDbContext db, OrderService orders) : ShopPageModel
{
    public Quote Quote { get; set; } = new();

    public async Task OnGetAsync() => Quote = await orders.QuoteAsync(Shop.Lines);

    /// <summary>Contenu du tiroir panier (chargé en JavaScript).</summary>
    public async Task<IActionResult> OnGetMiniAsync() => Partial("_CartMini", await orders.QuoteAsync(Shop.Lines));

    public async Task<IActionResult> OnPostAddAsync(int productId, int qty = 1)
    {
        var p = await db.Products.FindAsync(productId);
        if (p == null || !p.IsActive) return Result(false, "Ce produit n'est plus disponible.");

        var wanted = Shop.QuantityOf(productId) + Math.Max(1, qty);
        if (p.Quantity <= 0) return Result(false, $"« {p.Name} » est épuisé.");
        if (wanted > p.Quantity)
        {
            Shop.Set(productId, p.Quantity);
            return Result(true, $"Stock limité : {p.Quantity} « {p.Name} » maximum dans le panier.");
        }
        Shop.Set(productId, wanted);
        return Result(true, $"« {p.Name} » ajouté au panier", p.Emoji);
    }

    public async Task<IActionResult> OnPostUpdateAsync(int productId, int qty)
    {
        var p = await db.Products.FindAsync(productId);
        if (p != null && qty > p.Quantity)
        {
            qty = p.Quantity;
            Shop.Set(productId, qty);
            return Result(true, $"Stock limité : {p.Quantity} « {p.Name} » maximum.");
        }
        Shop.Set(productId, qty);
        return Result(true, qty <= 0 ? "Article retiré du panier." : "Quantité mise à jour.");
    }

    public IActionResult OnPostRemove(int productId)
    {
        Shop.Set(productId, 0);
        return Result(true, "Article retiré du panier.");
    }

    public IActionResult OnPostClear()
    {
        Shop.Clear();
        return Result(true, "Panier vidé.");
    }

    private IActionResult Result(bool ok, string message, string? emoji = null)
    {
        if (IsAjax) return new JsonResult(new { ok, message, emoji, count = Shop.Count });
        Flash(message, ok ? "success" : "error");
        var back = Request.Headers.Referer.ToString();
        return Url.IsLocalUrl(back) || (Uri.TryCreate(back, UriKind.Absolute, out var u) && u.Host == Request.Host.Host)
            ? Redirect(back) : RedirectToPage();
    }
}
