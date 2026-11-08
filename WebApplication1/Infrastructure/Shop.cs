using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Models;
using Services;

namespace WebApplication1.Infrastructure;

/// <summary>
/// État de la boutique pour la requête en cours : client connecté (cookie « Customer »)
/// et panier (cookie « sm_cart » : "idProduit:qté|idProduit:qté").
/// </summary>
public class ShopSession(IHttpContextAccessor accessor)
{
    public const string Scheme = "Customer";
    private const string CartCookie = "sm_cart";
    private const int MaxLines = 60;
    private const int MaxQtyPerLine = 99;

    private HttpContext Ctx => accessor.HttpContext!;

    // ---------------- Client ----------------
    private ClaimsPrincipal? _customer;
    private bool _authLoaded;

    public async Task<ClaimsPrincipal?> GetCustomerAsync()
    {
        if (!_authLoaded)
        {
            var r = await Ctx.AuthenticateAsync(Scheme);
            _customer = r.Succeeded ? r.Principal : null;
            _authLoaded = true;
        }
        return _customer;
    }

    public async Task<int?> GetCustomerIdAsync() =>
        int.TryParse((await GetCustomerAsync())?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public async Task<string?> GetCustomerNameAsync() => (await GetCustomerAsync())?.FindFirstValue(ClaimTypes.Name);

    public async Task SignInAsync(Customer c, bool remember)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, c.Id.ToString()),
            new(ClaimTypes.Name, c.FullName),
            new(ClaimTypes.Email, c.Email ?? ""),
        };
        await Ctx.SignInAsync(Scheme, new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme)),
            new AuthenticationProperties { IsPersistent = remember });
    }

    public Task SignOutAsync() => Ctx.SignOutAsync(Scheme);

    // ---------------- Panier ----------------
    private List<CartLine>? _lines;

    public List<CartLine> Lines => _lines ??= Parse(Ctx.Request.Cookies[CartCookie]);
    public int Count => Lines.Sum(l => l.Quantity);
    public int QuantityOf(int productId) => Lines.FirstOrDefault(l => l.ProductId == productId)?.Quantity ?? 0;

    public void Add(int productId, int qty) => Set(productId, QuantityOf(productId) + qty);

    public void Set(int productId, int qty)
    {
        var lines = Lines.ToList();
        var idx = lines.FindIndex(l => l.ProductId == productId);
        qty = Math.Min(qty, MaxQtyPerLine);
        if (qty <= 0) { if (idx >= 0) lines.RemoveAt(idx); }
        else if (idx >= 0) lines[idx] = new CartLine(productId, qty);
        else lines.Add(new CartLine(productId, qty));
        _lines = lines.Take(MaxLines).ToList();
        Save();
    }

    public void Clear()
    {
        _lines = [];
        Ctx.Response.Cookies.Delete(CartCookie);
    }

    private void Save() => Ctx.Response.Cookies.Append(CartCookie,
        string.Join('|', Lines.Select(l => $"{l.ProductId}:{l.Quantity}")),
        new CookieOptions { Expires = DateTimeOffset.Now.AddDays(30), HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax });

    private static List<CartLine> Parse(string? raw)
    {
        var result = new List<CartLine>();
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split(':');
            if (kv.Length == 2 && int.TryParse(kv[0], out var id) && int.TryParse(kv[1], out var q) && id > 0 && q > 0
                && result.All(r => r.ProductId != id))
                result.Add(new CartLine(id, Math.Min(q, MaxQtyPerLine)));
        }
        return result.Take(MaxLines).ToList();
    }
}

/// <summary>Page de base de la boutique.</summary>
public abstract class ShopPageModel : PageModel
{
    protected ShopSession Shop => HttpContext.RequestServices.GetRequiredService<ShopSession>();

    /// <summary>Requête envoyée par le JavaScript (ajout au panier sans recharger la page).</summary>
    protected bool IsAjax => Request.Headers["X-Requested-With"] == "fetch";

    protected void Flash(string message, string kind = "success")
    {
        TempData["shop_flash"] = message;
        TempData["shop_flash_kind"] = kind;
    }
}

/// <summary>Une carte produit de la boutique.</summary>
public record ProductCardVm(Product Product, decimal Discount, string Color)
{
    public decimal FinalPrice => CatalogService.Discounted(Product.Price, Discount);
    public bool OutOfStock => Product.Quantity <= 0;
}

public static class ShopHelpers
{
    public static async Task<List<ProductCardVm>> ToCardsAsync(this IEnumerable<Product> products, CatalogService catalog)
    {
        var discounts = await catalog.GetActiveDiscountsAsync();
        var result = new List<ProductCardVm>();
        foreach (var p in products)
        {
            var node = await catalog.FindAsync(p.CategoryId);
            result.Add(new ProductCardVm(p, discounts.GetValueOrDefault(p.Id), node?.Color ?? CatalogService.DefaultColor));
        }
        return result;
    }

    public static string Money(this decimal v) => v.ToString("N2") + " DH";

    /// <summary>Liste déroulante des catégories, groupées par rayon principal.</summary>
    public static async Task<List<SelectListItem>> CategoryOptionsAsync(this CatalogService catalog, int? selected = null, int? exclude = null)
    {
        var items = new List<SelectListItem>();
        foreach (var root in await catalog.GetTreeAsync())
        {
            if (root.Id == exclude) continue;
            var group = new SelectListGroup { Name = root.Name };
            foreach (var n in root.SelfAndDescendants())
            {
                if (n.Id == exclude || n.Ancestors().Any(a => a.Id == exclude)) continue;
                items.Add(new SelectListItem
                {
                    Value = n.Id.ToString(),
                    Text = n.Depth == 0 ? $"{n.Name} (tout le rayon)" : new string(' ', (n.Depth - 1) * 2) + n.Name,
                    Group = group,
                    Selected = n.Id == selected,
                });
            }
        }
        return items;
    }
}
