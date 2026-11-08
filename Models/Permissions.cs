namespace Models;

/// <summary>
/// Liste de toutes les permissions du système.
/// Chaque rôle possède un sous-ensemble de ces permissions.
/// </summary>
public static class Permissions
{
    public const string DashboardView = "dashboard.view";

    public const string ProductsView = "products.view";
    public const string ProductsCreate = "products.create";
    public const string ProductsEdit = "products.edit";
    public const string ProductsDelete = "products.delete";

    public const string CategoriesView = "categories.view";
    public const string CategoriesManage = "categories.manage";

    public const string StockView = "stock.view";
    public const string StockIn = "stock.in";
    public const string StockOut = "stock.out";

    public const string PromotionsView = "promotions.view";
    public const string PromotionsManage = "promotions.manage";

    public const string CustomersView = "customers.view";
    public const string CustomersManage = "customers.manage";

    public const string SalesView = "sales.view";
    public const string SalesCreate = "sales.create";

    public const string OrdersView = "orders.view";
    public const string OrdersManage = "orders.manage";

    public const string AnalyticsView = "analytics.view";
    public const string AuditView = "audit.view";

    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";

    public record PermissionInfo(string Key, string Label);
    public record PermissionGroup(string Name, PermissionInfo[] Items);

    /// <summary>Permissions regroupées pour l'affichage (écran des rôles).</summary>
    public static readonly PermissionGroup[] Groups =
    [
        new("Tableau de bord", [new(DashboardView, "Voir le tableau de bord")]),
        new("Produits", [
            new(ProductsView, "Voir les produits"),
            new(ProductsCreate, "Ajouter des produits"),
            new(ProductsEdit, "Modifier des produits"),
            new(ProductsDelete, "Supprimer des produits")]),
        new("Catégories", [
            new(CategoriesView, "Voir les catégories"),
            new(CategoriesManage, "Gérer les catégories")]),
        new("Stock", [
            new(StockView, "Voir les mouvements de stock"),
            new(StockIn, "Entrée de stock (ajouter quantité)"),
            new(StockOut, "Sortie de stock (retirer quantité)")]),
        new("Promotions", [
            new(PromotionsView, "Voir les promotions"),
            new(PromotionsManage, "Gérer les promotions")]),
        new("Clients", [
            new(CustomersView, "Voir les clients"),
            new(CustomersManage, "Gérer les clients")]),
        new("Ventes", [
            new(SalesView, "Voir les ventes"),
            new(SalesCreate, "Enregistrer une vente (caisse)")]),
        new("Commandes en ligne", [
            new(OrdersView, "Voir les commandes de la boutique"),
            new(OrdersManage, "Traiter les commandes (statut, livraison, annulation)")]),
        new("Analyse & Contrôle", [
            new(AnalyticsView, "Voir les analyses"),
            new(AuditView, "Voir le journal d'activité")]),
        new("Administration", [
            new(UsersManage, "Gérer les employés (comptes)"),
            new(RolesManage, "Gérer les rôles et accès")]),
    ];

    public static IEnumerable<string> All => Groups.SelectMany(g => g.Items).Select(i => i.Key);

    public static string LabelOf(string key) =>
        Groups.SelectMany(g => g.Items).FirstOrDefault(i => i.Key == key)?.Label ?? key;
}
