using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Stock;

public class MoveModel(AppDbContext db, StockService stock) : AppPageModel
{
    [BindProperty(SupportsGet = true)]
    [Range(1, int.MaxValue, ErrorMessage = "Choisissez un produit")]
    public int ProductId { get; set; }

    [BindProperty] public StockMovementType Type { get; set; } = StockMovementType.In;

    [BindProperty, Range(1, 100_000, ErrorMessage = "Quantité entre 1 et 100 000")]
    public int Quantity { get; set; } = 1;

    [BindProperty, StringLength(250)]
    public string? Reason { get; set; }

    public List<Product> Products { get; set; } = [];

    private async Task LoadAsync() =>
        Products = await db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Access.CanAny(Permissions.StockIn, Permissions.StockOut)) return Forbid();
        if (!Can(Permissions.StockIn)) Type = StockMovementType.Out;
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Type is not (StockMovementType.In or StockMovementType.Out)) return BadRequest();
        if (Require(Type == StockMovementType.In ? Permissions.StockIn : Permissions.StockOut) is { } denied) return denied;

        if (Type == StockMovementType.Out && string.IsNullOrWhiteSpace(Reason))
            ModelState.AddModelError(nameof(Reason), "Indiquez le motif de la sortie (casse, périmé, retour fournisseur…).");

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var (ok, message) = await stock.MoveAsync(ProductId, Type, Quantity, Reason);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, message);
            await LoadAsync();
            return Page();
        }

        Success(message);
        return RedirectToPage("/Products/Details", new { id = ProductId });
    }
}
