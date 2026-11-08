using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Customers;

[HasPermission(Permissions.CustomersView)]
public class DetailsModel(AppDbContext db) : AppPageModel
{
    public record FavoriteProduct(string Name, int Quantity, decimal Spent);

    public Customer Customer { get; set; } = null!;
    public List<Sale> Sales { get; set; } = [];
    public decimal TotalSpent { get; set; }
    public decimal AverageBasket { get; set; }
    public double? AvgDaysBetweenVisits { get; set; }
    public List<FavoriteProduct> Favorites { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        Customer = c;

        Sales = await db.Sales.AsNoTracking().Include(s => s.User).Include(s => s.Items).ThenInclude(i => i.Product)
            .Where(s => s.CustomerId == id).OrderByDescending(s => s.Date).ToListAsync();

        TotalSpent = Sales.Sum(s => s.Total);
        AverageBasket = Sales.Count > 0 ? TotalSpent / Sales.Count : 0;
        if (Sales.Count > 1)
            AvgDaysBetweenVisits = (Sales.First().Date - Sales.Last().Date).TotalDays / (Sales.Count - 1);

        Favorites = Sales.SelectMany(s => s.Items)
            .GroupBy(i => i.Product?.Name ?? "?")
            .Select(g => new FavoriteProduct(g.Key, g.Sum(i => i.Quantity), g.Sum(i => i.LineTotal)))
            .OrderByDescending(f => f.Quantity).Take(5).ToList();

        return Page();
    }
}
