using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Admin.Users;

/// <summary>Fiche employé : tout ce qu'il a fait (journal), ses mouvements de stock et ses ventes.</summary>
[HasPermission(Permissions.AuditView)]
public class DetailsModel(AppDbContext db) : AppPageModel
{
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true, Name = "act")] public string? ActionFilter { get; set; }

    public AppUser Employee { get; set; } = null!;
    public PagedList<AuditLog> Activity { get; set; } = new();
    public List<string> Actions { get; set; } = [];
    public int SalesCount30 { get; set; }
    public decimal SalesTotal30 { get; set; }
    public int StockIn30 { get; set; }
    public int StockOut30 { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var u = await db.Users.AsNoTracking().Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return NotFound();
        Employee = u;

        var since = DateTime.Today.AddDays(-29);
        var sales = await db.Sales.Where(s => s.UserId == id && s.Date >= since).Select(s => s.Total).ToListAsync();
        SalesCount30 = sales.Count;
        SalesTotal30 = sales.Sum();

        var moves = await db.StockMovements.Where(m => m.UserId == id && m.Date >= since && m.Type != StockMovementType.Sale)
            .Select(m => m.Quantity).ToListAsync();
        StockIn30 = moves.Where(q => q > 0).Sum();
        StockOut30 = -moves.Where(q => q < 0).Sum();

        Actions = await db.AuditLogs.Where(a => a.UserId == id).Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();

        var q = db.AuditLogs.AsNoTracking().Where(a => a.UserId == id);
        if (!string.IsNullOrEmpty(ActionFilter)) q = q.Where(a => a.Action == ActionFilter);
        Activity = await q.OrderByDescending(a => a.Id).ToPagedAsync(PageNumber, 30);
        return Page();
    }
}
