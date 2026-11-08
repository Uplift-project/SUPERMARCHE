using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Shop.Account;

public class IndexModel(AppDbContext db) : ShopPageModel
{
    public class ProfileInput
    {
        [Required(ErrorMessage = "Indiquez votre nom"), StringLength(100)] public string FullName { get; set; } = "";
        [Required(ErrorMessage = "Indiquez votre téléphone"), RegularExpression(@"^\+?[0-9 .-]{9,15}$", ErrorMessage = "Numéro invalide")]
        public string Phone { get; set; } = "";
        [StringLength(250)] public string? Address { get; set; }
        [StringLength(60)] public string? City { get; set; }
    }

    public Customer Customer { get; set; } = null!;
    public List<Order> Orders { get; set; } = [];
    [BindProperty] public ProfileInput Input { get; set; } = new();

    private async Task<IActionResult?> LoadAsync()
    {
        var id = await Shop.GetCustomerIdAsync();
        if (id == null) return RedirectToPage("/Shop/Account/Login", new { returnUrl = "/compte" });
        var c = await db.Customers.FindAsync(id.Value);
        if (c == null)
        {
            await Shop.SignOutAsync();
            return RedirectToPage("/Shop/Account/Login");
        }
        Customer = c;
        Orders = await db.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => o.CustomerId == c.Id).OrderByDescending(o => o.CreatedAt).ToListAsync();
        return null;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await LoadAsync() is { } r) return r;
        Input = new ProfileInput { FullName = Customer.FullName, Phone = Customer.Phone ?? "", Address = Customer.Address, City = Customer.City };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await LoadAsync() is { } r) return r;
        if (!ModelState.IsValid) return Page();
        Customer.FullName = Input.FullName.Trim();
        Customer.Phone = Input.Phone.Trim();
        Customer.Address = Input.Address?.Trim();
        Customer.City = Input.City?.Trim();
        await db.SaveChangesAsync();
        await Shop.SignInAsync(Customer, true);
        Flash("Vos informations ont été mises à jour.");
        return RedirectToPage();
    }
}
