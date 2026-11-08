using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Models;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Categories;

[HasPermission(Permissions.CategoriesView)]
public class IndexModel(AppDbContext db, CatalogService catalog) : AppPageModel
{
    public record Row(CategoryNode Node, int ProductCount, int StockUnits, decimal StockValue);

    public List<Row> Rows { get; set; } = [];
    public List<SelectListItem> RootOptions { get; set; } = [];
    [BindProperty] public Category Input { get; set; } = new();

    public static readonly string[] Palette = ["#dc2626", "#d97706", "#2563eb", "#059669", "#db2777", "#7c3aed", "#0891b2", "#65a30d", "#475569"];

    public async Task OnGetAsync()
    {
        var stats = (await db.Products.AsNoTracking().Where(p => p.IsActive)
                .Select(p => new { p.CategoryId, p.Quantity, p.CostPrice }).ToListAsync())
            .GroupBy(p => p.CategoryId)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Units: g.Sum(x => x.Quantity), Value: g.Sum(x => x.Quantity * x.CostPrice)));

        foreach (var n in await catalog.GetFlatAsync())
        {
            var all = n.SelfAndDescendants().Select(d => stats.GetValueOrDefault(d.Id)).ToList();
            Rows.Add(new Row(n, all.Sum(s => s.Count), all.Sum(s => s.Units), all.Sum(s => s.Value)));
        }
        RootOptions = (await catalog.GetTreeAsync()).Select(r => new SelectListItem(r.Name, r.Id.ToString())).ToList();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (Require(Permissions.CategoriesManage) is { } denied) return denied;

        Input.Name = Input.Name?.Trim() ?? "";
        if (string.IsNullOrEmpty(Input.Name))
        {
            Error("Le nom de la catégorie est obligatoire.");
            return RedirectToPage();
        }
        if (await db.Categories.AnyAsync(c => c.Name == Input.Name && c.Id != Input.Id))
        {
            Error($"La catégorie « {Input.Name} » existe déjà.");
            return RedirectToPage();
        }
        if (Input.ParentId is 0) Input.ParentId = null;
        if (Input.ParentId != null && Input.ParentId == Input.Id)
        {
            Error("Une catégorie ne peut pas être sa propre parente.");
            return RedirectToPage();
        }
        if (Input.ParentId != null && Input.Id != 0 && (await catalog.SelfAndDescendantIdsAsync(Input.Id)).Contains(Input.ParentId.Value))
        {
            Error("Impossible : la catégorie parente choisie est une de ses sous-catégories.");
            return RedirectToPage();
        }

        var slug = Slug.From(Input.Name);
        var baseSlug = slug;
        for (var i = 2; await db.Categories.AnyAsync(c => c.Slug == slug && c.Id != Input.Id); i++) slug = $"{baseSlug}-{i}";

        var color = string.IsNullOrWhiteSpace(Input.Color) || Input.ParentId != null && Input.Color == "#000000" ? null : Input.Color;
        var icon = string.IsNullOrWhiteSpace(Input.Icon) ? null : Input.Icon.Trim().StartsWith("bi-") ? Input.Icon.Trim() : "bi-" + Input.Icon.Trim();

        Category cat;
        if (Input.Id == 0)
        {
            cat = new Category();
            db.Categories.Add(cat);
        }
        else
        {
            cat = await db.Categories.FindAsync(Input.Id) ?? throw new InvalidOperationException();
        }
        cat.Name = Input.Name;
        cat.Slug = slug;
        cat.Description = Input.Description;
        cat.ParentId = Input.ParentId;
        cat.Color = color;
        cat.Icon = icon;
        cat.SortOrder = Input.SortOrder;
        await db.SaveChangesAsync();
        Success($"Catégorie « {Input.Name} » {(Input.Id == 0 ? "ajoutée" : "modifiée")}.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (Require(Permissions.CategoriesManage) is { } denied) return denied;

        var cat = await db.Categories.FindAsync(id);
        if (cat == null) return NotFound();
        if (await db.Categories.AnyAsync(c => c.ParentId == id))
        {
            Error($"« {cat.Name} » contient des sous-catégories : supprimez-les ou déplacez-les d'abord.");
            return RedirectToPage();
        }
        if (await db.Products.AnyAsync(p => p.CategoryId == id))
        {
            Error($"« {cat.Name} » contient des produits : déplacez-les d'abord vers une autre catégorie.");
            return RedirectToPage();
        }
        db.Categories.Remove(cat);
        await db.SaveChangesAsync();
        Success($"Catégorie « {cat.Name} » supprimée.");
        return RedirectToPage();
    }
}
