using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;

namespace Services;

public record CartLine(int ProductId, int Quantity);

public record QuoteLine(Product Product, int Quantity, decimal DiscountPercent, decimal UnitPrice, decimal FinalUnitPrice, decimal LineTotal)
{
    public bool OutOfStock => Product.Quantity < Quantity;
}

public class Quote
{
    public List<QuoteLine> Lines { get; } = [];
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Total => SubTotal - Discount + DeliveryFee;
    public int ItemCount => Lines.Sum(l => l.Quantity);
    public bool IsEmpty => Lines.Count == 0;
    public decimal FreeDeliveryThreshold => DbSeeder.FreeDeliveryThreshold;
    public decimal RemainingForFreeDelivery => Math.Max(0, FreeDeliveryThreshold - (SubTotal - Discount));
}

public class CheckoutInput
{
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public DateTime DeliveryDate { get; set; }
    public string DeliverySlot { get; set; } = "";
    public PaymentMethod PaymentMethod { get; set; }
    public string? Notes { get; set; }
}

public class OrderService(AppDbContext db, StockService stock, CatalogService catalog)
{
    public static readonly string[] DeliverySlots = DbSeeder.DeliverySlots;
    public static readonly string[] Cities = ["Casablanca", "Rabat", "Salé", "Témara", "Mohammedia", "Marrakech", "Fès", "Tanger", "Agadir", "Meknès", "Kénitra", "Oujda"];

    /// <summary>Calcule les prix (promos comprises) et les frais de livraison d'un panier.</summary>
    public async Task<Quote> QuoteAsync(IEnumerable<CartLine> lines)
    {
        var grouped = lines.Where(l => l.Quantity > 0).GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var quote = new Quote();
        if (grouped.Count == 0) return quote;

        var ids = grouped.Keys.ToList();
        var products = await db.Products.AsNoTracking().Include(p => p.Category)
            .Where(p => ids.Contains(p.Id) && p.IsActive).ToListAsync();
        var discounts = await catalog.GetActiveDiscountsAsync();

        foreach (var p in products.OrderBy(p => ids.IndexOf(p.Id)))
        {
            var qty = grouped[p.Id];
            var disc = discounts.GetValueOrDefault(p.Id);
            var final = CatalogService.Discounted(p.Price, disc);
            var line = new QuoteLine(p, qty, disc, p.Price, final, final * qty);
            quote.Lines.Add(line);
            quote.SubTotal += p.Price * qty;
            quote.Discount += (p.Price - final) * qty;
        }
        quote.DeliveryFee = quote.SubTotal - quote.Discount >= DbSeeder.FreeDeliveryThreshold ? 0 : DbSeeder.DeliveryFeeAmount;
        return quote;
    }

    public async Task<(bool Ok, string Message, Order? Order)> PlaceAsync(CheckoutInput input, IEnumerable<CartLine> lines, int? customerId)
    {
        var quote = await QuoteAsync(lines);
        if (quote.IsEmpty) return (false, "Votre panier est vide.", null);
        if (input.DeliveryDate.Date < DateTime.Today)
            return (false, "Choisissez une date de livraison à partir d'aujourd'hui.", null);

        // Stock relu en mode suivi pour la mise à jour
        var ids = quote.Lines.Select(l => l.Product.Id).ToList();
        var tracked = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        foreach (var l in quote.Lines)
        {
            var p = tracked[l.Product.Id];
            if (p.Quantity < l.Quantity)
                return (false, p.Quantity == 0
                    ? $"« {p.Name} » est en rupture de stock. Retirez-le du panier pour continuer."
                    : $"Il ne reste que {p.Quantity} « {p.Name} ». Ajustez la quantité.", null);
        }

        Customer? customer = customerId is > 0 ? await db.Customers.FindAsync(customerId.Value) : null;
        if (customer == null)
        {
            // Client invité : on le retrouve par téléphone/email, sinon on le crée (fiche client côté admin).
            var phone = input.Phone.Trim();
            var email = input.Email?.Trim().ToLowerInvariant();
            customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone || (email != null && c.Email == email));
            if (customer == null)
            {
                customer = new Customer { FullName = input.FullName.Trim(), Phone = phone, Email = email, CreatedAt = DateTime.Now };
                db.Customers.Add(customer);
            }
        }
        customer.Address ??= input.Address.Trim();
        customer.City ??= input.City.Trim();

        var order = new Order
        {
            Number = await NextNumberAsync(),
            Customer = customer,
            FullName = input.FullName.Trim(),
            Phone = input.Phone.Trim(),
            Email = input.Email?.Trim(),
            Address = input.Address.Trim(),
            City = input.City.Trim(),
            DeliveryDate = input.DeliveryDate.Date,
            DeliverySlot = input.DeliverySlot,
            PaymentMethod = input.PaymentMethod,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.Now,
            SubTotal = quote.SubTotal,
            Discount = quote.Discount,
            DeliveryFee = quote.DeliveryFee,
            Total = quote.Total,
        };
        foreach (var l in quote.Lines)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = l.Product.Id, ProductName = l.Product.Name, Quantity = l.Quantity,
                UnitPrice = l.UnitPrice, DiscountPercent = l.DiscountPercent, LineTotal = l.LineTotal,
            });
            // Le stock est réservé dès la commande (rendu en cas d'annulation).
            stock.Apply(tracked[l.Product.Id], StockMovementType.Order, -l.Quantity, $"Commande {order.Number}");
        }

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return (true, $"Commande {order.Number} enregistrée.", order);
    }

    /// <summary>Étapes autorisées depuis chaque statut.</summary>
    public static OrderStatus[] NextStatuses(OrderStatus s) => s switch
    {
        OrderStatus.Pending => [OrderStatus.Confirmed, OrderStatus.Cancelled],
        OrderStatus.Confirmed => [OrderStatus.Preparing, OrderStatus.Cancelled],
        OrderStatus.Preparing => [OrderStatus.Shipping, OrderStatus.Cancelled],
        OrderStatus.Shipping => [OrderStatus.Delivered, OrderStatus.Cancelled],
        _ => [],
    };

    public async Task<(bool Ok, string Message)> ChangeStatusAsync(int orderId, OrderStatus status)
    {
        var order = await db.Orders.Include(o => o.Items).Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return (false, "Commande introuvable.");
        if (!NextStatuses(order.Status).Contains(status))
            return (false, $"Impossible de passer de « {order.Status.Label()} » à « {status.Label()} ».");

        if (status == OrderStatus.Cancelled)
        {
            var ids = order.Items.Select(i => i.ProductId).ToList();
            var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            foreach (var i in order.Items)
                if (products.TryGetValue(i.ProductId, out var p))
                    stock.Apply(p, StockMovementType.OrderCancel, i.Quantity, $"Annulation commande {order.Number}");
        }
        if (status == OrderStatus.Delivered && order.Customer != null)
            order.Customer.LoyaltyPoints += (int)(order.Total / 10);

        order.Status = status;
        order.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return (true, $"Commande {order.Number} : {status.Label()}.");
    }

    /// <summary>Le client annule lui-même tant que la commande n'est pas préparée.</summary>
    public async Task<(bool Ok, string Message)> CancelByCustomerAsync(int orderId, int customerId)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);
        if (order == null) return (false, "Commande introuvable.");
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
            return (false, "Cette commande est déjà en préparation : contactez le service client pour l'annuler.");
        return await ChangeStatusAsync(orderId, OrderStatus.Cancelled);
    }

    private async Task<string> NextNumberAsync()
    {
        var prefix = $"CMD{DateTime.Now:yyMMdd}-";
        var last = await db.Orders.Where(o => o.Number.StartsWith(prefix))
            .OrderByDescending(o => o.Number).Select(o => o.Number).FirstOrDefaultAsync();
        var n = last != null && int.TryParse(last[prefix.Length..], out var x) ? x + 1 : 1;
        return prefix + n.ToString("000");
    }
}

/// <summary>Comptes clients de la boutique (inscription / connexion).</summary>
public class ShopAccountService(AppDbContext db)
{
    private static readonly PasswordHasher<Customer> Hasher = new();

    public async Task<Customer?> LoginAsync(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();
        var c = await db.Customers.FirstOrDefaultAsync(x => x.Email == email && x.PasswordHash != null);
        if (c == null || Hasher.VerifyHashedPassword(c, c.PasswordHash!, password) == PasswordVerificationResult.Failed)
            return null;
        return c;
    }

    public async Task<(bool Ok, string Message, Customer? Customer)> RegisterAsync(string fullName, string email, string phone, string password)
    {
        email = email.Trim().ToLowerInvariant();
        phone = phone.Trim();
        var existing = await db.Customers.FirstOrDefaultAsync(c => c.Email == email);
        if (existing?.PasswordHash != null) return (false, "Un compte existe déjà avec cet email.", null);

        // Client déjà connu en magasin (même email ou téléphone) → on lui active un compte.
        existing ??= await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone && c.PasswordHash == null);
        var customer = existing ?? new Customer { CreatedAt = DateTime.Now };
        customer.FullName = fullName.Trim();
        customer.Email = email;
        customer.Phone = phone;
        customer.PasswordHash = Hasher.HashPassword(customer, password);
        if (existing == null) db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return (true, "Compte créé.", customer);
    }
}
