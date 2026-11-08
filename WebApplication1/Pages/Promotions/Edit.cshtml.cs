using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Promotions;

[HasPermission(Permissions.PromotionsManage)]
public class EditModel(AppDbContext db, Services.CatalogService catalog) : AppPageModel
{
    [BindProperty] public Promotion Promotion { get; set; } = new();
    [BindProperty] public string Target { get; set; } = "product";

    public SelectList Products { get; set; } = null!;
    public List<SelectListItem> Categories { get; set; } = [];
    public bool IsNew => Promotion.Id == 0;

    private async Task LoadAsync()
    {
        Products = new SelectList(await db.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(), "Id", "Name");
        // Une promo sur un rayon principal s'applique à toutes ses sous-catégories.
        Categories = await catalog.CategoryOptionsAsync(Promotion.CategoryId);
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is > 0)
        {
            var p = await db.Promotions.FindAsync(id.Value);
            if (p == null) return NotFound();
            Promotion = p;
            Target = p.CategoryId != null ? "category" : "product";
        }
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Remove("Promotion.Product");
        ModelState.Remove("Promotion.Category");

        if (Target == "category") Promotion.ProductId = null; else Promotion.CategoryId = null;
        if (Target == "category" && Promotion.CategoryId is null or 0)
            ModelState.AddModelError("Promotion.CategoryId", "Choisissez une catégorie");
        if (Target != "category" && Promotion.ProductId is null or 0)
            ModelState.AddModelError("Promotion.ProductId", "Choisissez un produit");
        if (Promotion.EndDate.Date < Promotion.StartDate.Date)
            ModelState.AddModelError("Promotion.EndDate", "La date de fin doit être après la date de début");

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        Promotion.StartDate = Promotion.StartDate.Date;
        Promotion.EndDate = Promotion.EndDate.Date;

        if (IsNew)
            db.Promotions.Add(Promotion);
        else
        {
            var existing = await db.Promotions.FindAsync(Promotion.Id);
            if (existing == null) return NotFound();
            db.Entry(existing).CurrentValues.SetValues(Promotion);
        }
        await db.SaveChangesAsync();
        Success($"Promotion « {Promotion.Name} » enregistrée.");
        return RedirectToPage("Index");
    }
}
