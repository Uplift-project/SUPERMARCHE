using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;

namespace Services;

public record SaleLineInput(int ProductId, int Quantity);

public class SaleService(AppDbContext db, StockService stock, ICurrentUser currentUser, CatalogService catalog)
{
    /// <summary>Meilleure remise active (produit, catégorie ou rayon parent) pour chaque produit.</summary>
    public Task<Dictionary<int, decimal>> GetActiveDiscountsAsync() => catalog.GetActiveDiscountsAsync();

    public async Task<(bool Ok, string Message, int? SaleId)> CreateAsync(int? customerId, IEnumerable<SaleLineInput> lines)
    {
        var grouped = lines.Where(l => l.Quantity > 0)
            .GroupBy(l => l.ProductId)
            .Select(g => new SaleLineInput(g.Key, g.Sum(x => x.Quantity)))
            .ToList();
        if (grouped.Count == 0) return (false, "Ajoutez au moins un produit.", null);

        var ids = grouped.Select(g => g.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id) && p.IsActive).ToDictionaryAsync(p => p.Id);
        var discounts = await GetActiveDiscountsAsync();

        Customer? customer = null;
        if (customerId is > 0)
        {
            customer = await db.Customers.FindAsync(customerId.Value);
            if (customer == null) return (false, "Client introuvable.", null);
        }

        var sale = new Sale
        {
            Number = await NextNumberAsync(),
            Customer = customer,
            UserId = currentUser.Id,
            Date = DateTime.Now,
        };

        foreach (var line in grouped)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                return (false, "Un des produits n'existe plus ou est désactivé.", null);
            if (product.Quantity < line.Quantity)
                return (false, $"Stock insuffisant pour « {product.Name} » (disponible : {product.Quantity}).", null);

            var disc = discounts.GetValueOrDefault(product.Id);
            var lineTotal = Math.Round(product.Price * line.Quantity * (1 - disc / 100m), 2);

            sale.Items.Add(new SaleItem
            {
                ProductId = product.Id,
                Quantity = line.Quantity,
                UnitPrice = product.Price,
                DiscountPercent = disc,
                LineTotal = lineTotal,
            });
            sale.SubTotal += product.Price * line.Quantity;
            sale.Total += lineTotal;

            stock.Apply(product, StockMovementType.Sale, -line.Quantity, $"Vente {sale.Number}");
        }
        sale.Discount = sale.SubTotal - sale.Total;

        // 1 point de fidélité pour chaque 10 DH dépensés
        if (customer != null) customer.LoyaltyPoints += (int)(sale.Total / 10);

        db.Sales.Add(sale);
        var log = db.AddAudit(AuditActions.Sale, "Vente", null,
            $"Vente {sale.Number} : {sale.Items.Sum(i => i.Quantity)} article(s), total {sale.Total:0.00} DH"
            + (customer != null ? $" — client {customer.FullName}" : ""));
        await db.SaveChangesAsync();
        log.EntityId = sale.Id.ToString();
        await db.SaveChangesAsync();

        return (true, $"Vente {sale.Number} enregistrée ({sale.Total:0.00} DH).", sale.Id);
    }

    private async Task<string> NextNumberAsync()
    {
        var prefix = $"V{DateTime.Now:yyMMdd}-";
        var count = await db.Sales.CountAsync(s => s.Number.StartsWith(prefix));
        return prefix + (count + 1).ToString("0000");
    }
}
