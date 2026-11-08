using Models;
using Services.Data;

namespace Services;

public class StockService(AppDbContext db, ICurrentUser currentUser)
{
    /// <summary>
    /// Ajoute (In) ou retire (Out) une quantité du stock d'un produit.
    /// Enregistre le mouvement + une ligne dans le journal (qui / quand / combien).
    /// </summary>
    public async Task<(bool Ok, string Message)> MoveAsync(int productId, StockMovementType type, int quantity, string? reason)
    {
        if (quantity <= 0) return (false, "La quantité doit être supérieure à 0.");

        var product = await db.Products.FindAsync(productId);
        if (product == null) return (false, "Produit introuvable.");

        var delta = type == StockMovementType.In ? quantity : -quantity;
        if (product.Quantity + delta < 0)
            return (false, $"Stock insuffisant : il reste seulement {product.Quantity} {product.Unit}.");

        var movement = Apply(product, type, delta, reason);

        var action = delta > 0 ? AuditActions.StockIn : AuditActions.StockOut;
        db.AddAudit(action, "Produit", product.Id,
            $"{(delta > 0 ? "+" : "")}{delta} {product.Unit} « {product.Name} » ({movement.QuantityBefore} → {movement.QuantityAfter})"
            + (string.IsNullOrWhiteSpace(reason) ? "" : $" — {reason}"),
            new Dictionary<string, string?[]> { ["Quantité"] = [movement.QuantityBefore.ToString(), movement.QuantityAfter.ToString()] });

        await db.SaveChangesAsync();
        return (true, $"Stock de « {product.Name} » mis à jour : {movement.QuantityAfter} {product.Unit}.");
    }

    /// <summary>Modifie la quantité et crée le mouvement (sans sauvegarder).</summary>
    internal StockMovement Apply(Product product, StockMovementType type, int delta, string? reason)
    {
        var movement = new StockMovement
        {
            Product = product,
            Type = type,
            Quantity = delta,
            QuantityBefore = product.Quantity,
            QuantityAfter = product.Quantity + delta,
            Reason = reason?.Trim(),
            UserId = currentUser.Id,
            Date = DateTime.Now,
        };
        product.Quantity += delta;
        db.StockMovements.Add(movement);
        return movement;
    }
}
