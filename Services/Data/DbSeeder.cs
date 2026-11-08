using Microsoft.EntityFrameworkCore;
using Models;

namespace Services.Data;

/// <summary>
/// Crée la base SQL Server au premier lancement et y met le strict nécessaire pour démarrer :
/// les rôles, un compte administrateur et les rayons / sous-catégories.
/// Aucun produit, aucune vente, aucun client inventés : tout s'ajoute depuis l'admin.
/// </summary>
public static class DbSeeder
{
    public const string AdminEmail = "admin@supermarche.ma";
    public const string DefaultPassword = "Admin@123";

    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Roles.AnyAsync()) return;

        db.SuppressAudit = true;

        // ---------------- Rôles ----------------
        var superAdmin = new Role
        {
            Name = "Super Admin",
            Description = "Accès total : gère les employés, les rôles et voit tout le journal.",
            IsSuperAdmin = true,
            PermissionSet = Permissions.All.ToHashSet(),
        };
        var stockManager = new Role
        {
            Name = "Gestionnaire de stock",
            Description = "Gère les produits, les catégories et les entrées/sorties de stock.",
            PermissionSet =
            [
                Permissions.DashboardView, Permissions.ProductsView, Permissions.ProductsCreate, Permissions.ProductsEdit,
                Permissions.CategoriesView, Permissions.CategoriesManage,
                Permissions.StockView, Permissions.StockIn, Permissions.StockOut, Permissions.PromotionsView,
                Permissions.OrdersView,
            ],
        };
        var cashier = new Role
        {
            Name = "Caissier",
            Description = "Enregistre les ventes, les clients et prépare les commandes en ligne.",
            PermissionSet =
            [
                Permissions.ProductsView, Permissions.SalesView, Permissions.SalesCreate,
                Permissions.CustomersView, Permissions.CustomersManage, Permissions.PromotionsView,
                Permissions.OrdersView, Permissions.OrdersManage,
            ],
        };
        var marketing = new Role
        {
            Name = "Responsable marketing",
            Description = "Gère les promotions, suit les clients et les analyses.",
            PermissionSet =
            [
                Permissions.DashboardView, Permissions.ProductsView, Permissions.CategoriesView,
                Permissions.PromotionsView, Permissions.PromotionsManage,
                Permissions.CustomersView, Permissions.CustomersManage,
                Permissions.SalesView, Permissions.AnalyticsView, Permissions.OrdersView,
            ],
        };
        db.Roles.AddRange(superAdmin, stockManager, cashier, marketing);

        // ---------------- Compte administrateur ----------------
        var admin = new AppUser { FullName = "Administrateur", Email = AdminEmail, Role = superAdmin };
        admin.PasswordHash = AccountService.HashPassword(admin, DefaultPassword);
        db.Users.Add(admin);

        // ---------------- Rayons → sous-catégories (les produits s'ajoutent depuis l'admin) ----------------
        var order = 0;
        foreach (var dept in Catalog)
        {
            var parent = new Category
            {
                Name = dept.Name, Slug = Slug.From(dept.Name), Description = dept.Description,
                Color = dept.Color, Icon = dept.Icon, SortOrder = order++,
            };
            db.Categories.Add(parent);

            var subOrder = 0;
            foreach (var sub in dept.Subs)
            {
                var cat = new Category
                {
                    Name = sub.Name, Slug = Slug.From(sub.Name), Parent = parent,
                    Icon = sub.Icon, SortOrder = subOrder++, Description = $"{sub.Name} — rayon {dept.Name.ToLower()}",
                };
                db.Categories.Add(cat);
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            Action = AuditActions.Create, EntityType = "Système", UserName = "Système",
            Description = "Base de données créée : rôles, compte administrateur, rayons et sous-catégories (aucun produit)",
        });

        await db.SaveChangesAsync();
        db.SuppressAudit = false;
    }

    // ------------------------------------------------------------------
    //  Paramètres de livraison (utilisés aussi par OrderService)
    // ------------------------------------------------------------------
    public const decimal FreeDeliveryThreshold = 300m;
    public const decimal DeliveryFeeAmount = 20m;
    public static readonly string[] DeliverySlots = ["09h – 12h", "12h – 15h", "15h – 18h", "18h – 21h"];

    // ------------------------------------------------------------------
    //  Catalogue de départ (modifiable ensuite depuis l'admin)
    // ------------------------------------------------------------------
    private record Sub(string Name, string Icon);
    private record Dept(string Name, string Color, string Icon, string Description, Sub[] Subs);

    private static readonly Dept[] Catalog =
    [
        new("Alimentation", "#dc2626", "bi-basket2-fill", "Produits frais, épicerie et tout pour bien manger chaque jour.",
        [
            new("Fruits & Légumes", "bi-flower1"),
            new("Boucherie & Volaille", "bi-egg-fried"),
            new("Produits laitiers & Œufs", "bi-cup-straw"),
            new("Boulangerie & Pâtisserie", "bi-cake2"),
            new("Épicerie salée", "bi-box2"),
            new("Épicerie sucrée & Petit-déj", "bi-cup-hot"),
        ]),
        new("Boissons", "#d97706", "bi-cup-straw", "Eaux, jus, sodas, thé et café pour toute la famille.",
        [
            new("Eaux", "bi-droplet"),
            new("Jus & Sodas", "bi-cup"),
            new("Thé & Café", "bi-cup-hot"),
        ]),
        new("Beauté & Hygiène", "#2563eb", "bi-stars", "Soins visage, cheveux, corps et parfums.",
        [
            new("Soins visage", "bi-emoji-smile"),
            new("Soins cheveux", "bi-wind"),
            new("Hygiène corporelle", "bi-droplet-half"),
            new("Hygiène bucco-dentaire", "bi-emoji-grin"),
            new("Parfums & Maquillage", "bi-gem"),
        ]),
        new("Entretien & Maison", "#059669", "bi-house-heart", "Lessive, produits ménagers et essentiels de la maison.",
        [
            new("Lessive", "bi-water"),
            new("Nettoyage", "bi-stars"),
            new("Papier & Essuie-tout", "bi-receipt"),
        ]),
        new("Bébé", "#db2777", "bi-balloon-heart", "Couches, repas et soins pour les tout-petits.",
        [
            new("Couches", "bi-heart"),
            new("Repas bébé", "bi-cup"),
        ]),
    ];
}
