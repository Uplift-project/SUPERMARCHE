using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Admin.Audit;

[HasPermission(Permissions.AuditView)]
public class IndexModel(AppDbContext db) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int? UserId { get; set; }
    [BindProperty(SupportsGet = true, Name = "act")] public string? ActionFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? Entity { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedList<AuditLog> Logs { get; set; } = new();
    public SelectList Users { get; set; } = null!;
    public List<string> Actions { get; set; } = [];
    public List<string> Entities { get; set; } = [];

    public async Task OnGetAsync()
    {
        Users = new SelectList(await db.Users.OrderBy(u => u.FullName).ToListAsync(), "Id", "FullName");
        Actions = await db.AuditLogs.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();
        Entities = await db.AuditLogs.Select(a => a.EntityType).Distinct().OrderBy(a => a).ToListAsync();

        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q)) q = q.Where(a => a.Description.Contains(Q));
        if (UserId is > 0) q = q.Where(a => a.UserId == UserId);
        if (!string.IsNullOrEmpty(ActionFilter)) q = q.Where(a => a.Action == ActionFilter);
        if (!string.IsNullOrEmpty(Entity)) q = q.Where(a => a.EntityType == Entity);
        if (From != null) q = q.Where(a => a.Timestamp >= From.Value.Date);
        if (To != null) q = q.Where(a => a.Timestamp < To.Value.Date.AddDays(1));

        Logs = await q.OrderByDescending(a => a.Id).ToPagedAsync(PageNumber, 50);
    }
}
