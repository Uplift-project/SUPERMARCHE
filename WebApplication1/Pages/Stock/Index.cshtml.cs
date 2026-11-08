using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Stock;

[HasPermission(Permissions.StockView)]
public class IndexModel(AppDbContext db) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public StockMovementType? Type { get; set; }
    [BindProperty(SupportsGet = true)] public int? UserId { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedList<StockMovement> Movements { get; set; } = new();
    public SelectList Users { get; set; } = null!;
    public int TotalIn { get; set; }
    public int TotalOut { get; set; }

    public async Task OnGetAsync()
    {
        Users = new SelectList(await db.Users.OrderBy(u => u.FullName).ToListAsync(), "Id", "FullName");

        var q = db.StockMovements.AsNoTracking().Include(m => m.Product).Include(m => m.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q)) q = q.Where(m => m.Product!.Name.Contains(Q));
        if (Type != null) q = q.Where(m => m.Type == Type);
        if (UserId is > 0) q = q.Where(m => m.UserId == UserId);
        if (From != null) q = q.Where(m => m.Date >= From.Value.Date);
        if (To != null) q = q.Where(m => m.Date < To.Value.Date.AddDays(1));

        TotalIn = await q.Where(m => m.Quantity > 0).SumAsync(m => m.Quantity);
        TotalOut = -await q.Where(m => m.Quantity < 0).SumAsync(m => m.Quantity);
        Movements = await q.OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).ToPagedAsync(PageNumber, 40);
    }
}
