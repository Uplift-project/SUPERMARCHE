using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Sales;

[HasPermission(Permissions.SalesView)]
public class DetailsModel(AppDbContext db) : AppPageModel
{
    public Sale Sale { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var s = await db.Sales.AsNoTracking()
            .Include(x => x.Customer).Include(x => x.User)
            .Include(x => x.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        Sale = s;
        return Page();
    }
}
