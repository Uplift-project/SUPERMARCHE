using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;

namespace Services;

/// <summary>Une catégorie avec ses sous-catégories et sa couleur effective (héritée du rayon).</summary>
public class CategoryNode
{
    public required Category Category { get; init; }
    public CategoryNode? Parent { get; set; }
    public List<CategoryNode> Children { get; } = [];
    public string Color { get; set; } = CatalogService.DefaultColor;
    public string Icon { get; set; } = "bi-tag";
    public int Depth { get; set; }

    public int Id => Category.Id;
    public string Name => Category.Name;
    public string Slug => Category.Slug;

    /// <summary>Le rayon principal (racine) de cette catégorie.</summary>
    public CategoryNode Root => Parent?.Root ?? this;

    /// <summary>Chemin complet « Alimentation › Fruits & Légumes ».</summary>
    public string Path => Parent == null ? Name : $"{Parent.Path} › {Name}";

    public IEnumerable<CategoryNode> SelfAndDescendants() => Children.SelectMany(c => c.SelfAndDescendants()).Prepend(this);
    public IEnumerable<CategoryNode> Ancestors() => Parent == null ? [] : Parent.Ancestors().Append(Parent);
}

/// <summary>Accès au catalogue hiérarchique et aux prix promotionnels (lu une fois par requête).</summary>
public class CatalogService(AppDbContext db)
{
    public const string DefaultColor = "#1f7a4d";

    private List<CategoryNode>? _roots;
    private Dictionary<int, CategoryNode>? _byId;
    private Dictionary<int, decimal>? _discounts;

    public async Task<List<CategoryNode>> GetTreeAsync()
    {
        if (_roots != null) return _roots;

        var all = await db.Categories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();
        _byId = all.ToDictionary(c => c.Id, c => new CategoryNode { Category = c });
        foreach (var node in _byId.Values)
        {
            if (node.Category.ParentId is { } pid && _byId.TryGetValue(pid, out var parent))
            {
                node.Parent = parent;
                parent.Children.Add(node);
            }
        }
        _roots = _byId.Values.Where(n => n.Parent == null).ToList();

        void Resolve(CategoryNode n, int depth)
        {
            n.Depth = depth;
            n.Color = !string.IsNullOrWhiteSpace(n.Category.Color) ? n.Category.Color! : n.Parent?.Color ?? DefaultColor;
            n.Icon = !string.IsNullOrWhiteSpace(n.Category.Icon) ? n.Category.Icon! : n.Parent?.Icon ?? "bi-tag";
            foreach (var c in n.Children) Resolve(c, depth + 1);
        }
        foreach (var r in _roots) Resolve(r, 0);
        return _roots;
    }

    public async Task<CategoryNode?> FindAsync(int id)
    {
        await GetTreeAsync();
        return _byId!.GetValueOrDefault(id);
    }

    public async Task<CategoryNode?> FindBySlugAsync(string slug)
    {
        await GetTreeAsync();
        return _byId!.Values.FirstOrDefault(n => string.Equals(n.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Toutes les catégories à plat, dans l'ordre de l'arbre (pour les listes déroulantes).</summary>
    public async Task<List<CategoryNode>> GetFlatAsync() =>
        (await GetTreeAsync()).SelectMany(r => r.SelfAndDescendants()).ToList();

    /// <summary>Id de la catégorie + de toutes ses sous-catégories.</summary>
    public async Task<List<int>> SelfAndDescendantIdsAsync(int id) =>
        (await FindAsync(id))?.SelfAndDescendants().Select(n => n.Id).ToList() ?? [id];

    /// <summary>
    /// Meilleure remise active pour chaque produit : promo sur le produit, sur sa catégorie
    /// ou sur n'importe quelle catégorie parente (ex. promo sur tout le rayon « Alimentation »).
    /// </summary>
    public async Task<Dictionary<int, decimal>> GetActiveDiscountsAsync()
    {
        if (_discounts != null) return _discounts;

        var today = DateTime.Today;
        var promos = await db.Promotions.AsNoTracking()
            .Where(p => p.IsActive && p.StartDate <= today && p.EndDate >= today)
            .ToListAsync();
        _discounts = [];
        if (promos.Count == 0) return _discounts;

        await GetTreeAsync();
        var byProduct = promos.Where(p => p.ProductId != null).GroupBy(p => p.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.Max(p => p.DiscountPercent));
        var byCategory = promos.Where(p => p.ProductId == null && p.CategoryId != null).GroupBy(p => p.CategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.Max(p => p.DiscountPercent));

        var products = await db.Products.AsNoTracking().Where(p => p.IsActive)
            .Select(p => new { p.Id, p.CategoryId }).ToListAsync();
        foreach (var p in products)
        {
            var best = byProduct.GetValueOrDefault(p.Id);
            if (_byId!.TryGetValue(p.CategoryId, out var node))
                foreach (var c in node.Ancestors().Append(node))
                    best = Math.Max(best, byCategory.GetValueOrDefault(c.Id));
            if (best > 0) _discounts[p.Id] = best;
        }
        return _discounts;
    }

    public static decimal Discounted(decimal price, decimal discountPercent) =>
        Math.Round(price * (1 - discountPercent / 100m), 2);
}
