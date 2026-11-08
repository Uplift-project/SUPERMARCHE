using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Analytics;

[HasPermission(Permissions.AnalyticsView)]
public class IndexModel(AppDbContext db) : AppPageModel
{
    public record RankRow(string Name, int Count, decimal Amount, int? Id = null);

    [BindProperty(SupportsGet = true)] public int Days { get; set; } = 30;

    public decimal Revenue { get; set; }
    public decimal GrossMargin { get; set; }
    public int SalesCount { get; set; }
    public int UniqueCustomers { get; set; }
    public double IdentifiedShare { get; set; }

    public List<string> DayLabels { get; set; } = [];
    public List<decimal> DayValues { get; set; } = [];
    public List<string> HourLabels { get; set; } = [];
    public List<int> HourValues { get; set; } = [];
    public List<RankRow> ByCategory { get; set; } = [];
    public List<RankRow> TopProducts { get; set; } = [];
    public List<RankRow> ByEmployee { get; set; } = [];
    public List<RankRow> TopCustomers { get; set; } = [];
    public List<Product> SleepingProducts { get; set; } = [];

    public async Task OnGetAsync()
    {
        Days = Days is 7 or 30 or 90 ? Days : 30;
        var from = DateTime.Today.AddDays(-(Days - 1));

        var sales = await db.Sales.AsNoTracking()
            .Include(s => s.User).Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Category)
            .Where(s => s.Date >= from).ToListAsync();
        var items = sales.SelectMany(s => s.Items).ToList();

        Revenue = sales.Sum(s => s.Total);
        SalesCount = sales.Count;
        GrossMargin = items.Sum(i => i.LineTotal - (i.Product?.CostPrice ?? 0) * i.Quantity);
        UniqueCustomers = sales.Where(s => s.CustomerId != null).Select(s => s.CustomerId).Distinct().Count();
        IdentifiedShare = SalesCount == 0 ? 0 : sales.Count(s => s.CustomerId != null) * 100.0 / SalesCount;

        for (var d = from; d <= DateTime.Today; d = d.AddDays(1))
        {
            var day = d;
            DayLabels.Add(day.ToString("dd/MM"));
            DayValues.Add(Math.Round(sales.Where(s => s.Date.Date == day).Sum(s => s.Total), 2));
        }

        for (var h = 8; h <= 22; h++)
        {
            var hour = h;
            HourLabels.Add($"{hour}h");
            HourValues.Add(sales.Count(s => s.Date.Hour == hour));
        }

        ByCategory = items.GroupBy(i => i.Product?.Category?.Name ?? "?")
            .Select(g => new RankRow(g.Key, g.Sum(i => i.Quantity), Math.Round(g.Sum(i => i.LineTotal), 2)))
            .OrderByDescending(r => r.Amount).ToList();

        TopProducts = items.GroupBy(i => i.ProductId)
            .Select(g => new RankRow(g.First().Product?.Name ?? "?", g.Sum(i => i.Quantity), g.Sum(i => i.LineTotal), g.Key))
            .OrderByDescending(r => r.Amount).Take(10).ToList();

        ByEmployee = sales.GroupBy(s => s.UserId)
            .Select(g => new RankRow(g.First().User?.FullName ?? "(supprimé)", g.Count(), g.Sum(s => s.Total), g.Key))
            .OrderByDescending(r => r.Amount).ToList();

        TopCustomers = sales.Where(s => s.CustomerId != null).GroupBy(s => s.CustomerId!.Value)
            .Select(g => new RankRow(g.First().Customer!.FullName, g.Count(), g.Sum(s => s.Total), g.Key))
            .OrderByDescending(r => r.Amount).Take(8).ToList();

        var soldIds = items.Select(i => i.ProductId).ToHashSet();
        SleepingProducts = (await db.Products.AsNoTracking().Include(p => p.Category).Where(p => p.IsActive).ToListAsync())
            .Where(p => !soldIds.Contains(p.Id)).OrderBy(p => p.Name).ToList();
    }
}
