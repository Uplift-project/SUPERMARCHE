using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Orders;

[HasPermission(Permissions.OrdersView)]
public class DetailsModel(AppDbContext db, OrderService orders) : AppPageModel
{
    public Order Order { get; set; } = null!;
    public List<AuditLog> History { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var o = await db.Orders.AsNoTracking().Include(x => x.Items).ThenInclude(i => i.Product).Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();
        Order = o;
        if (Can(Permissions.AuditView))
            History = await db.AuditLogs.AsNoTracking()
                .Where(a => a.EntityType == "Commande" && a.EntityId == id.ToString())
                .OrderByDescending(a => a.Id).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, OrderStatus status)
    {
        if (Require(Permissions.OrdersManage) is { } denied) return denied;
        var (ok, message) = await orders.ChangeStatusAsync(id, status);
        if (ok) Success(message); else Error(message);
        return RedirectToPage(new { id });
    }
}
