using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages;

public class IndexModel(AppDbContext db) : AppPageModel
{
    public decimal RevenueToday { get; set; }
    public int SalesToday { get; set; }
    public decimal Revenue30 { get; set; }
    public decimal RevenuePrev30 { get; set; }
    public int CustomersCount { get; set; }
    public int NewCustomers30 { get; set; }
    public int LowStockCount { get; set; }
    public int ActivePromotions { get; set; }

    public List<string> ChartLabels { get; set; } = [];
    public List<decimal> ChartValues { get; set; } = [];

    public List<Product> LowStock { get; set; } = [];
    public List<Sale> LatestSales { get; set; } = [];
    public List<AuditLog> LatestActivity { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        // Un employé sans accès au tableau de bord est envoyé vers sa première page autorisée.
        if (!Can(Permissions.DashboardView))
        {
            var target = new (string Perm, string Page)[]
            {
                (Permissions.SalesCreate, "/Sales/New"), (Permissions.OrdersView, "/Orders/Index"), (Permissions.SalesView, "/Sales/Index"),
                (Permissions.ProductsView, "/Products/Index"), (Permissions.StockView, "/Stock/Index"),
                (Permissions.CustomersView, "/Customers/Index"), (Permissions.PromotionsView, "/Promotions/Index"),
                (Permissions.CategoriesView, "/Categories/Index"), (Permissions.AnalyticsView, "/Analytics/Index"),
                (Permissions.AuditView, "/Admin/Audit/Index"), (Permissions.UsersManage, "/Admin/Users/Index"),
                (Permissions.RolesManage, "/Admin/Roles/Index"),
            }.FirstOrDefault(t => Can(t.Perm)).Page;
            return target != null ? RedirectToPage(target) : RedirectToPage("/Account/Profile");
        }

        var today = DateTime.Today;
        var from = today.AddDays(-59);
        var sales = await db.Sales.AsNoTracking().Where(s => s.Date >= from)
            .Select(s => new { s.Date, s.Total }).ToListAsync();

        var todaySales = sales.Where(s => s.Date >= today).ToList();
        RevenueToday = todaySales.Sum(s => s.Total);
        SalesToday = todaySales.Count;
        Revenue30 = sales.Where(s => s.Date >= today.AddDays(-29)).Sum(s => s.Total);
        RevenuePrev30 = sales.Where(s => s.Date < today.AddDays(-29)).Sum(s => s.Total);

        for (var d = today.AddDays(-13); d <= today; d = d.AddDays(1))
        {
            var day = d;
            ChartLabels.Add(day.ToString("dd/MM"));
            ChartValues.Add(Math.Round(sales.Where(s => s.Date.Date == day).Sum(s => s.Total), 2));
        }

        CustomersCount = await db.Customers.CountAsync();
        NewCustomers30 = await db.Customers.CountAsync(c => c.CreatedAt >= today.AddDays(-29));
        LowStockCount = await db.Products.CountAsync(p => p.IsActive && p.Quantity <= p.MinQuantity);
        ActivePromotions = await db.Promotions.CountAsync(p => p.IsActive && p.StartDate <= today && p.EndDate >= today);

        LowStock = await db.Products.AsNoTracking().Include(p => p.Category)
            .Where(p => p.IsActive && p.Quantity <= p.MinQuantity)
            .OrderBy(p => p.Quantity).Take(8).ToListAsync();

        if (Can(Permissions.SalesView))
            LatestSales = await db.Sales.AsNoTracking().Include(s => s.Customer).Include(s => s.User)
                .OrderByDescending(s => s.Date).Take(6).ToListAsync();

        if (Can(Permissions.AuditView))
            LatestActivity = await db.AuditLogs.AsNoTracking().OrderByDescending(a => a.Id).Take(8).ToListAsync();

        return Page();
    }
}
