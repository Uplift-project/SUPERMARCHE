using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Orders;

[HasPermission(Permissions.OrdersView)]
public class IndexModel(AppDbContext db) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public OrderStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? Day { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedList<Order> Orders { get; set; } = new();
    public Dictionary<OrderStatus, int> CountByStatus { get; set; } = [];
    public decimal DeliveredToday { get; set; }
    public int ToDeliverToday { get; set; }

    public async Task OnGetAsync()
    {
        CountByStatus = await db.Orders.GroupBy(o => o.Status).Select(g => new { g.Key, C = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.C);
        var today = DateTime.Today;
        ToDeliverToday = await db.Orders.CountAsync(o => o.DeliveryDate == today && o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled);
        DeliveredToday = (await db.Orders.Where(o => o.Status == OrderStatus.Delivered && o.UpdatedAt >= today)
            .Select(o => o.Total).ToListAsync()).Sum();

        var q = db.Orders.AsNoTracking().Include(o => o.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
            q = q.Where(o => o.Number.Contains(Q) || o.FullName.Contains(Q) || o.Phone.Contains(Q) || o.City.Contains(Q));
        if (Status != null) q = q.Where(o => o.Status == Status);
        if (Day != null) q = q.Where(o => o.DeliveryDate == Day.Value.Date);

        Orders = await q.OrderBy(o => o.Status == OrderStatus.Delivered || o.Status == OrderStatus.Cancelled)
            .ThenByDescending(o => o.CreatedAt).ToPagedAsync(PageNumber);
    }
}
