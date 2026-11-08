using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Sales;

[HasPermission(Permissions.SalesCreate)]
public class NewModel(AppDbContext db, SaleService sales) : AppPageModel
{
    public class LineInput
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public record ProductOption(int Id, string Name, string? Barcode, decimal Price, decimal Discount, int Stock, string Unit, string Category);

    [BindProperty] public int? CustomerId { get; set; }
    [BindProperty] public List<LineInput> Lines { get; set; } = [];

    public List<ProductOption> Products { get; set; } = [];
    public List<Customer> Customers { get; set; } = [];

    private async Task LoadAsync()
    {
        var discounts = await sales.GetActiveDiscountsAsync();
        Products = (await db.Products.AsNoTracking().Include(p => p.Category)
                .Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync())
            .Select(p => new ProductOption(p.Id, p.Name, p.Barcode, p.Price, discounts.GetValueOrDefault(p.Id),
                p.Quantity, p.Unit, p.Category?.Name ?? ""))
            .ToList();
        Customers = await db.Customers.AsNoTracking().OrderBy(c => c.FullName).ToListAsync();
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var (ok, message, saleId) = await sales.CreateAsync(CustomerId,
            Lines.Select(l => new SaleLineInput(l.ProductId, l.Quantity)));

        if (!ok)
        {
            Error(message);
            await LoadAsync();
            return Page();
        }

        Success(message);
        return Can(Permissions.SalesView)
            ? RedirectToPage("Details", new { id = saleId })
            : RedirectToPage();
    }
}
