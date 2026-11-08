using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Shop;

public class ConfirmationModel(AppDbContext db) : ShopPageModel
{
    public Order Order { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(string number)
    {
        // Visible juste après la commande, ou par le client propriétaire connecté.
        // Les URL sont en minuscules : les numéros sont stockés en majuscules (CMD…).
        number = number.ToUpperInvariant();
        var customerId = await Shop.GetCustomerIdAsync();
        var justPlaced = TempData.Peek("last_order") as string == number;

        var order = await db.Orders.AsNoTracking().Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Number == number);
        if (order == null || (!justPlaced && (customerId == null || order.CustomerId != customerId))) return NotFound();
        Order = order;
        return Page();
    }
}
