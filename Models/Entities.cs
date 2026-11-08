using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models;

// ============================================================
//  Utilisateurs & Rôles
// ============================================================

public class Role
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom est obligatoire")]
    [StringLength(60)]
    [Display(Name = "Nom du rôle")]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    /// <summary>Permissions séparées par des virgules.</summary>
    public string Permissions { get; set; } = string.Empty;

    /// <summary>Le Super Admin a tous les droits et ne peut pas être supprimé.</summary>
    public bool IsSuperAdmin { get; set; }

    public List<AppUser> Users { get; set; } = [];

    [NotMapped]
    public HashSet<string> PermissionSet
    {
        get => Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        set => Permissions = string.Join(',', value.OrderBy(p => p));
    }
}

public class AppUser
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom est obligatoire")]
    [StringLength(100)]
    [Display(Name = "Nom complet")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'email est obligatoire")]
    [EmailAddress(ErrorMessage = "Email invalide")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Téléphone")]
    public string? Phone { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Change à chaque modification sensible → déconnecte les sessions ouvertes.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    [Display(Name = "Rôle")]
    public int RoleId { get; set; }
    public Role? Role { get; set; }

    [Display(Name = "Compte actif")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLoginAt { get; set; }
}

// ============================================================
//  Catalogue & Stock
// ============================================================

/// <summary>
/// Catégorie hiérarchique : un rayon principal (ex. « Alimentation ») contient des sous-catégories
/// (ex. « Fruits & Légumes »). Chaque rayon principal a sa couleur, héritée par ses sous-catégories.
/// </summary>
public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom est obligatoire")]
    [StringLength(80)]
    [Display(Name = "Nom")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Identifiant dans l'URL de la boutique (ex. « fruits-legumes »).</summary>
    [StringLength(90)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    [Display(Name = "Catégorie parente")]
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public List<Category> Children { get; set; } = [];

    /// <summary>Couleur du rayon (#RRGGBB). Vide → hérite de la catégorie parente.</summary>
    [StringLength(9)]
    [Display(Name = "Couleur")]
    public string? Color { get; set; }

    /// <summary>Icône Bootstrap Icons (ex. « bi-basket2 »).</summary>
    [StringLength(40)]
    [Display(Name = "Icône")]
    public string? Icon { get; set; }

    [Display(Name = "Ordre d'affichage")]
    public int SortOrder { get; set; }

    public List<Product> Products { get; set; } = [];
}

public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom est obligatoire")]
    [StringLength(120)]
    [Display(Name = "Nom du produit")]
    public string Name { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Code-barres")]
    public string? Barcode { get; set; }

    [Display(Name = "Catégorie")]
    [Range(1, int.MaxValue, ErrorMessage = "Choisissez une catégorie")]
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    [Display(Name = "Prix de vente (DH)")]
    [Range(0.01, 1_000_000, ErrorMessage = "Le prix de vente doit être supérieur à 0")]
    public decimal Price { get; set; }

    [Display(Name = "Prix d'achat (DH)")]
    [Range(0, 1_000_000, ErrorMessage = "Prix invalide")]
    public decimal CostPrice { get; set; }

    /// <summary>Quantité en stock. Modifiée uniquement via les mouvements de stock.</summary>
    [Display(Name = "Quantité en stock")]
    public int Quantity { get; set; }

    [Display(Name = "Seuil d'alerte")]
    [Range(0, 100_000)]
    public int MinQuantity { get; set; } = 5;

    [StringLength(20)]
    [Display(Name = "Unité")]
    public string Unit { get; set; } = "pièce";

    [Display(Name = "Actif")]
    public bool IsActive { get; set; } = true;

    [StringLength(1000)]
    [Display(Name = "Description (boutique)")]
    public string? Description { get; set; }

    /// <summary>Chemin de la photo (ex. /uploads/products/12.jpg).</summary>
    [StringLength(300)]
    [Display(Name = "Photo")]
    public string? ImageUrl { get; set; }

    /// <summary>Visuel de secours quand il n'y a pas de photo.</summary>
    [StringLength(16)]
    [Display(Name = "Emoji (si pas de photo)")]
    public string? Emoji { get; set; }

    [Display(Name = "Mis en avant sur la boutique")]
    public bool IsFeatured { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [NotMapped] public bool IsLowStock => Quantity <= MinQuantity;
}

public enum StockMovementType
{
    [Display(Name = "Entrée")] In = 1,
    [Display(Name = "Sortie")] Out = 2,
    [Display(Name = "Vente")] Sale = 3,
    [Display(Name = "Ajustement")] Adjustment = 4,
    [Display(Name = "Commande en ligne")] Order = 5,
    [Display(Name = "Annulation commande")] OrderCancel = 6,
}

public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public StockMovementType Type { get; set; }

    /// <summary>Quantité (positive pour entrée, négative pour sortie).</summary>
    public int Quantity { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }

    [StringLength(250)]
    public string? Reason { get; set; }

    public int? UserId { get; set; }
    public AppUser? User { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
}

// ============================================================
//  Promotions
// ============================================================

public class Promotion
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom est obligatoire")]
    [StringLength(120)]
    [Display(Name = "Nom de la promotion")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Remise (%)")]
    [Range(1, 90, ErrorMessage = "La remise doit être entre 1 et 90 %")]
    public decimal DiscountPercent { get; set; }

    /// <summary>Promotion sur un produit précis…</summary>
    [Display(Name = "Produit")]
    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>…ou sur toute une catégorie.</summary>
    [Display(Name = "Catégorie")]
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    [Display(Name = "Date début")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Display(Name = "Date fin")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [NotMapped]
    public bool IsRunning => IsActive && DateTime.Today >= StartDate.Date && DateTime.Today <= EndDate.Date;
}

// ============================================================
//  Clients & Ventes
// ============================================================

public class Customer
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom est obligatoire")]
    [StringLength(100)]
    [Display(Name = "Nom complet")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Téléphone")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Email invalide")]
    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(60)]
    [Display(Name = "Ville")]
    public string? City { get; set; }

    [Display(Name = "Points fidélité")]
    public int LoyaltyPoints { get; set; }

    [StringLength(250)]
    [Display(Name = "Adresse")]
    public string? Address { get; set; }

    /// <summary>Mot de passe du compte client sur la boutique (vide = client magasin sans compte).</summary>
    public string? PasswordHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<Sale> Sales { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
}

// ============================================================
//  Commandes en ligne (boutique + livraison)
// ============================================================

public enum OrderStatus
{
    [Display(Name = "En attente")] Pending = 1,
    [Display(Name = "Confirmée")] Confirmed = 2,
    [Display(Name = "En préparation")] Preparing = 3,
    [Display(Name = "En livraison")] Shipping = 4,
    [Display(Name = "Livrée")] Delivered = 5,
    [Display(Name = "Annulée")] Cancelled = 9,
}

public enum PaymentMethod
{
    [Display(Name = "Paiement à la livraison (espèces)")] CashOnDelivery = 1,
    [Display(Name = "Carte bancaire à la livraison (TPE)")] CardOnDelivery = 2,
}

public class Order
{
    public int Id { get; set; }

    [StringLength(30)]
    [Display(Name = "N° commande")]
    public string Number { get; set; } = string.Empty;

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Nom complet")]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(30)]
    [Display(Name = "Téléphone")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Email { get; set; }

    [Required, StringLength(250)]
    [Display(Name = "Adresse")]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(60)]
    [Display(Name = "Ville")]
    public string City { get; set; } = string.Empty;

    [Display(Name = "Date de livraison")]
    public DateTime DeliveryDate { get; set; }

    [StringLength(30)]
    [Display(Name = "Créneau")]
    public string DeliverySlot { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Instructions")]
    public string? Notes { get; set; }

    [Display(Name = "Paiement")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;

    [Display(Name = "Statut")]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }

    [Display(Name = "Frais de livraison")]
    public decimal DeliveryFee { get; set; }
    public decimal Total { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Nom copié au moment de la commande.</summary>
    [StringLength(120)]
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal LineTotal { get; set; }
}

public class Sale
{
    public int Id { get; set; }

    [StringLength(30)]
    public string Number { get; set; } = string.Empty;

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Employé (caissier) qui a enregistré la vente.</summary>
    public int? UserId { get; set; }
    public AppUser? User { get; set; }

    public DateTime Date { get; set; } = DateTime.Now;
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }

    public List<SaleItem> Items { get; set; } = [];
}

public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal LineTotal { get; set; }
}

// ============================================================
//  Journal d'activité (qui a fait quoi)
// ============================================================

public static class AuditActions
{
    public const string Create = "Création";
    public const string Update = "Modification";
    public const string Delete = "Suppression";
    public const string StockIn = "Entrée stock";
    public const string StockOut = "Sortie stock";
    public const string Sale = "Vente";
    public const string Order = "Commande";
    public const string Login = "Connexion";
    public const string LoginFailed = "Échec connexion";
    public const string Logout = "Déconnexion";
    public const string PasswordReset = "Mot de passe";
}

public class AuditLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public int? UserId { get; set; }

    /// <summary>Nom copié au moment de l'action (reste lisible même si l'employé est supprimé).</summary>
    [StringLength(100)]
    public string UserName { get; set; } = "Système";

    [StringLength(40)]
    public string Action { get; set; } = string.Empty;

    [StringLength(40)]
    public string EntityType { get; set; } = string.Empty;

    [StringLength(40)]
    public string? EntityId { get; set; }

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Détail JSON des changements : { "Champ": ["ancienne", "nouvelle"] }.</summary>
    public string? Changes { get; set; }

    [StringLength(60)]
    public string? IpAddress { get; set; }
}
