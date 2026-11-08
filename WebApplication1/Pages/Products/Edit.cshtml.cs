using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Models;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Products;

[HasPermission(Permissions.ProductsView)]
public class EditModel(AppDbContext db, StockService stock, CatalogService catalog, IWebHostEnvironment env) : AppPageModel
{
    private static readonly string[] AllowedImages = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private const long MaxImageBytes = 4 * 1024 * 1024;

    [BindProperty] public Product Product { get; set; } = new();

    [BindProperty, Range(0, 100_000, ErrorMessage = "Quantité invalide")]
    [Display(Name = "Quantité initiale")]
    public int InitialQuantity { get; set; }

    [BindProperty, Display(Name = "Photo du produit")] public IFormFile? Photo { get; set; }
    [BindProperty] public bool RemovePhoto { get; set; }

    public List<SelectListItem> Categories { get; set; } = [];
    public bool IsNew => Product.Id == 0;

    private async Task LoadListsAsync() =>
        Categories = await catalog.CategoryOptionsAsync(Product.CategoryId);

    /// <summary>Enregistre la photo dans wwwroot/uploads/products et renvoie son URL.</summary>
    private async Task<string?> SavePhotoAsync(int productId)
    {
        if (Photo == null || Photo.Length == 0) return null;
        var ext = Path.GetExtension(Photo.FileName).ToLowerInvariant();
        var dir = Path.Combine(env.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(dir);
        var name = $"{productId}-{Guid.NewGuid():N}"[..20] + ext;
        await using (var fs = System.IO.File.Create(Path.Combine(dir, name)))
            await Photo.CopyToAsync(fs);
        return $"/uploads/products/{name}";
    }

    private void DeletePhoto(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/products/")) return;
        var path = Path.Combine(env.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { /* fichier verrouillé : ignoré */ }
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is > 0)
        {
            if (Require(Permissions.ProductsEdit) is { } denied) return denied;
            var p = await db.Products.FindAsync(id.Value);
            if (p == null) return NotFound();
            Product = p;
        }
        else if (Require(Permissions.ProductsCreate) is { } denied) return denied;

        await LoadListsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Require(IsNew ? Permissions.ProductsCreate : Permissions.ProductsEdit) is { } denied) return denied;

        ModelState.Remove("Product.Category");
        if (Photo != null)
        {
            if (!AllowedImages.Contains(Path.GetExtension(Photo.FileName).ToLowerInvariant()))
                ModelState.AddModelError(nameof(Photo), "Format accepté : JPG, PNG, WEBP ou GIF.");
            else if (Photo.Length > MaxImageBytes)
                ModelState.AddModelError(nameof(Photo), "La photo ne doit pas dépasser 4 Mo.");
        }
        if (!string.IsNullOrWhiteSpace(Product.Barcode) &&
            await db.Products.AnyAsync(p => p.Barcode == Product.Barcode && p.Id != Product.Id))
            ModelState.AddModelError("Product.Barcode", "Ce code-barres est déjà utilisé par un autre produit.");

        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            return Page();
        }

        Product.Barcode = string.IsNullOrWhiteSpace(Product.Barcode) ? null : Product.Barcode.Trim();
        Product.Name = Product.Name.Trim();
        Product.Description = string.IsNullOrWhiteSpace(Product.Description) ? null : Product.Description.Trim();
        Product.Emoji = string.IsNullOrWhiteSpace(Product.Emoji) ? null : Product.Emoji.Trim();

        if (IsNew)
        {
            Product.Quantity = 0;
            Product.CreatedAt = DateTime.Now;
            Product.ImageUrl = null;
            db.Products.Add(Product);
            await db.SaveChangesAsync();

            if (await SavePhotoAsync(Product.Id) is { } url)
            {
                Product.ImageUrl = url;
                await db.SaveChangesAsync();
            }

            if (InitialQuantity > 0)
                await stock.MoveAsync(Product.Id, StockMovementType.In, InitialQuantity, "Stock initial");

            Success($"Produit « {Product.Name} » ajouté.");
        }
        else
        {
            var existing = await db.Products.FindAsync(Product.Id);
            if (existing == null) return NotFound();

            // La quantité ne se modifie pas ici : uniquement par un mouvement de stock (traçabilité).
            existing.Name = Product.Name;
            existing.Barcode = Product.Barcode;
            existing.CategoryId = Product.CategoryId;
            existing.Price = Product.Price;
            existing.CostPrice = Product.CostPrice;
            existing.MinQuantity = Product.MinQuantity;
            existing.Unit = Product.Unit;
            existing.IsActive = Product.IsActive;
            existing.Description = Product.Description;
            existing.Emoji = Product.Emoji;
            existing.IsFeatured = Product.IsFeatured;

            if (await SavePhotoAsync(existing.Id) is { } url)
            {
                DeletePhoto(existing.ImageUrl);
                existing.ImageUrl = url;
            }
            else if (RemovePhoto)
            {
                DeletePhoto(existing.ImageUrl);
                existing.ImageUrl = null;
            }
            await db.SaveChangesAsync();
            Success($"Produit « {existing.Name} » modifié.");
        }

        return RedirectToPage("Details", new { id = Product.Id });
    }
}
