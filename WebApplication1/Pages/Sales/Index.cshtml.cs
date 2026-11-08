using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Sales;

[HasPermission(Permissions.SalesView)]
public class IndexModel(AppDbContext db) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int? UserId { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedList<Sale> Sales { get; set; } = new();
    public SelectList Users { get; set; } = null!;
    public decimal Total { get; set; }
    public int Count { get; set; }

    public async Task OnGetAsync()
    {
        Users = new SelectList(await db.Users.OrderBy(u => u.FullName).ToListAsync(), "Id", "FullName");

        var q = db.Sales.AsNoTracking().Include(s => s.Customer).Include(s => s.User).Include(s => s.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
            q = q.Where(s => s.Number.Contains(Q) || (s.Customer != null && s.Customer.FullName.Contains(Q)));
        if (UserId is > 0) q = q.Where(s => s.UserId == UserId);
        if (From != null) q = q.Where(s => s.Date >= From.Value.Date);
        if (To != null) q = q.Where(s => s.Date < To.Value.Date.AddDays(1));

        var totals = await q.Select(s => s.Total).ToListAsync();
        Total = totals.Sum();
        Count = totals.Count;
        Sales = await q.OrderByDescending(s => s.Date).ToPagedAsync(PageNumber, 30);
    }
}
