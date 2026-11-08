using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Customers;

[HasPermission(Permissions.CustomersView)]
public class IndexModel(AppDbContext db) : AppPageModel
{
    public record Row(Customer Customer, int Visits, decimal Spent, DateTime? LastVisit);

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? City { get; set; }
    [BindProperty(SupportsGet = true)] public string Sort { get; set; } = "spent";

    public List<Row> Rows { get; set; } = [];
    public List<string> Cities { get; set; } = [];

    public async Task OnGetAsync()
    {
        Cities = await db.Customers.Where(c => c.City != null).Select(c => c.City!).Distinct().OrderBy(c => c).ToListAsync();

        var q = db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
            q = q.Where(c => c.FullName.Contains(Q) || (c.Phone != null && c.Phone.Contains(Q)) || (c.Email != null && c.Email.Contains(Q)));
        if (!string.IsNullOrWhiteSpace(City)) q = q.Where(c => c.City == City);

        var customers = await q.ToListAsync();
        var ids = customers.Select(c => c.Id).ToList();
        var stats = (await db.Sales.AsNoTracking().Where(s => s.CustomerId != null && ids.Contains(s.CustomerId.Value))
                .Select(s => new { s.CustomerId, s.Total, s.Date }).ToListAsync())
            .GroupBy(s => s.CustomerId!.Value)
            .ToDictionary(g => g.Key, g => (Visits: g.Count(), Spent: g.Sum(x => x.Total), Last: (DateTime?)g.Max(x => x.Date)));

        Rows = customers.Select(c =>
        {
            var s = stats.GetValueOrDefault(c.Id);
            return new Row(c, s.Visits, s.Spent, s.Last);
        }).ToList();

        Rows = Sort switch
        {
            "name" => Rows.OrderBy(r => r.Customer.FullName).ToList(),
            "visits" => Rows.OrderByDescending(r => r.Visits).ToList(),
            "recent" => Rows.OrderByDescending(r => r.LastVisit).ToList(),
            "points" => Rows.OrderByDescending(r => r.Customer.LoyaltyPoints).ToList(),
            _ => Rows.OrderByDescending(r => r.Spent).ToList(),
        };
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (Require(Permissions.CustomersManage) is { } denied) return denied;
        var c = await db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        db.Customers.Remove(c);
        await db.SaveChangesAsync();
        Success($"Client « {c.FullName} » supprimé (ses ventes sont conservées en « client anonyme »).");
        return RedirectToPage();
    }
}
