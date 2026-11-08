using Microsoft.AspNetCore.Mvc;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Customers;

[HasPermission(Permissions.CustomersManage)]
public class EditModel(AppDbContext db) : AppPageModel
{
    [BindProperty] public Customer Customer { get; set; } = new();
    public bool IsNew => Customer.Id == 0;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is > 0)
        {
            var c = await db.Customers.FindAsync(id.Value);
            if (c == null) return NotFound();
            Customer = c;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (IsNew)
        {
            Customer.CreatedAt = DateTime.Now;
            Customer.LoyaltyPoints = 0;
            db.Customers.Add(Customer);
        }
        else
        {
            var c = await db.Customers.FindAsync(Customer.Id);
            if (c == null) return NotFound();
            c.FullName = Customer.FullName;
            c.Phone = Customer.Phone;
            c.Email = Customer.Email;
            c.City = Customer.City;
            // Seul le Super Admin peut corriger les points de fidélité à la main (et c'est journalisé).
            if (Access.IsSuperAdmin) c.LoyaltyPoints = Customer.LoyaltyPoints;
        }
        await db.SaveChangesAsync();
        Success($"Client « {Customer.FullName} » enregistré.");
        return RedirectToPage("Details", new { id = Customer.Id });
    }
}
