using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Models;

namespace Services.Data;

public class AppDbContext : DbContext
{
    private readonly ICurrentUser? _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser? currentUser = null) : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Désactive l'audit automatique (utilisé pendant le seed initial).</summary>
    public bool SuppressAudit { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Montants en DH et pourcentages : 2 décimales suffisent.
        builder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        b.Entity<AppUser>().HasOne(u => u.Role).WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<Role>().HasIndex(r => r.Name).IsUnique();
        b.Entity<Category>().HasIndex(c => c.Name).IsUnique();
        b.Entity<Category>().HasIndex(c => c.Slug).IsUnique();
        b.Entity<Category>().HasOne(c => c.Parent).WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<Order>().HasIndex(o => o.Number).IsUnique();
        b.Entity<Order>().HasIndex(o => o.CreatedAt);
        b.Entity<Order>().HasIndex(o => o.Status);
        b.Entity<Order>().HasOne(o => o.Customer).WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<OrderItem>().HasOne(i => i.Order).WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<OrderItem>().HasOne(i => i.Product).WithMany()
            .HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<Product>().HasIndex(p => p.Barcode).IsUnique();
        b.Entity<Product>().HasOne(p => p.Category).WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<StockMovement>().HasOne(m => m.Product).WithMany()
            .HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<StockMovement>().HasOne(m => m.User).WithMany()
            .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<StockMovement>().HasIndex(m => m.Date);

        b.Entity<Promotion>().HasOne(p => p.Product).WithMany()
            .HasForeignKey(p => p.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Promotion>().HasOne(p => p.Category).WithMany()
            .HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Sale>().HasIndex(s => s.Number).IsUnique();
        b.Entity<Sale>().HasIndex(s => s.Date);
        b.Entity<Sale>().HasOne(s => s.Customer).WithMany(c => c.Sales)
            .HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Sale>().HasOne(s => s.User).WithMany()
            .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<SaleItem>().HasOne(i => i.Sale).WithMany(s => s.Items)
            .HasForeignKey(i => i.SaleId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SaleItem>().HasOne(i => i.Product).WithMany()
            .HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<AuditLog>().HasIndex(a => a.Timestamp);
        b.Entity<AuditLog>().HasIndex(a => a.UserId);
    }

    // ------------------------------------------------------------------
    //  Journal manuel (connexion, mouvements de stock, ventes…)
    // ------------------------------------------------------------------

    public AuditLog AddAudit(string action, string entityType, object? entityId, string description,
        object? changes = null, int? userId = null, string? userName = null)
    {
        var log = new AuditLog
        {
            Timestamp = DateTime.Now,
            UserId = userId ?? _currentUser?.Id,
            UserName = userName ?? _currentUser?.Name ?? "Système",
            IpAddress = _currentUser?.IpAddress,
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            Description = Truncate(description, 500),
            Changes = changes == null ? null : JsonSerializer.Serialize(changes),
        };
        AuditLogs.Add(log);
        return log;
    }

    // ------------------------------------------------------------------
    //  Journal automatique : chaque création / modification / suppression
    // ------------------------------------------------------------------

    private static readonly Dictionary<Type, string> EntityLabels = new()
    {
        [typeof(Product)] = "Produit",
        [typeof(Category)] = "Catégorie",
        [typeof(Promotion)] = "Promotion",
        [typeof(Customer)] = "Client",
        [typeof(AppUser)] = "Employé",
        [typeof(Role)] = "Rôle",
        [typeof(Order)] = "Commande",
    };

    /// <summary>Champs jamais affichés dans le journal (sensibles ou gérés ailleurs).</summary>
    private static readonly HashSet<string> IgnoredProps =
        ["Id", "PasswordHash", "SecurityStamp", "LastLoginAt", "CreatedAt", "UpdatedAt", "Quantity"];

    public static string LabelOf(Type t) => EntityLabels.TryGetValue(t, out var l) ? l : t.Name;

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        if (SuppressAudit)
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);

        ChangeTracker.DetectChanges();
        var pending = new List<(EntityEntry Entry, AuditLog Log)>();

        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (!EntityLabels.ContainsKey(entry.Entity.GetType())) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var changes = new Dictionary<string, string?[]>();
            foreach (var prop in entry.Properties)
            {
                var name = prop.Metadata.Name;
                if (IgnoredProps.Contains(name)) continue;

                string? oldV = entry.State == EntityState.Added ? null : await FormatAsync(name, prop.OriginalValue, ct);
                string? newV = entry.State == EntityState.Deleted ? null : await FormatAsync(name, prop.CurrentValue, ct);

                if (entry.State == EntityState.Modified && oldV == newV) continue;
                if (entry.State != EntityState.Modified && string.IsNullOrEmpty(oldV ?? newV)) continue;

                changes[DisplayName(entry.Entity.GetType(), name)] = [oldV, newV];
            }

            if (entry.State == EntityState.Modified && changes.Count == 0) continue;

            var label = LabelOf(entry.Entity.GetType());
            var title = EntityTitle(entry.Entity);
            var (action, verb) = entry.State switch
            {
                EntityState.Added => (AuditActions.Create, "créé(e)"),
                EntityState.Deleted => (AuditActions.Delete, "supprimé(e)"),
                _ => (AuditActions.Update, "modifié(e)"),
            };

            var desc = $"{label} « {title} » {verb}";
            if (entry.State == EntityState.Modified)
                desc += " : " + string.Join(", ", changes.Select(c => $"{c.Key} {c.Value[0] ?? "∅"} → {c.Value[1] ?? "∅"}"));

            var log = AddAudit(action, label, null, desc, changes);
            pending.Add((entry, log));
        }

        if (pending.Count == 0)
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);

        // Les IDs des nouvelles entités ne sont connus qu'après l'enregistrement.
        var ownTx = Database.CurrentTransaction == null ? await Database.BeginTransactionAsync(ct) : null;
        try
        {
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
            foreach (var (entry, log) in pending)
                log.EntityId = entry.Property("Id").CurrentValue?.ToString();
            await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
            if (ownTx != null) await ownTx.CommitAsync(ct);
            return result;
        }
        finally
        {
            if (ownTx != null) await ownTx.DisposeAsync();
        }
    }

    private static string EntityTitle(object entity)
    {
        var t = entity.GetType();
        var v = t.GetProperty("Number")?.GetValue(entity) ?? t.GetProperty("Name")?.GetValue(entity) ?? t.GetProperty("FullName")?.GetValue(entity);
        return v?.ToString() ?? "#" + t.GetProperty("Id")?.GetValue(entity);
    }

    private static string DisplayName(Type t, string prop) =>
        t.GetProperty(prop)?.GetCustomAttribute<DisplayAttribute>()?.Name ?? prop;

    /// <summary>Formate une valeur ; les clés étrangères sont remplacées par le nom lisible.</summary>
    private async Task<string?> FormatAsync(string prop, object? value, CancellationToken ct)
    {
        if (value == null) return null;
        try
        {
            switch (prop)
            {
                case "CategoryId" when value is int id:
                    return (await Categories.FindAsync([id], ct))?.Name ?? $"#{id}";
                case "RoleId" when value is int id:
                    return (await Roles.FindAsync([id], ct))?.Name ?? $"#{id}";
                case "ProductId" when value is int id:
                    return (await Products.FindAsync([id], ct))?.Name ?? $"#{id}";
                case "ParentId" when value is int id:
                    return (await Categories.FindAsync([id], ct))?.Name ?? $"#{id}";
            }
        }
        catch { /* on garde l'id brut */ }

        return value switch
        {
            bool bo => bo ? "Oui" : "Non",
            DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("dd/MM/yyyy") : d.ToString("dd/MM/yyyy HH:mm"),
            decimal m => m.ToString("0.##"),
            _ => value.ToString(),
        };
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
