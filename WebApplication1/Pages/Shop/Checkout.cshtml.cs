using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Models;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Shop;

public class CheckoutModel(AppDbContext db, OrderService orders) : ShopPageModel
{
    public class InputModel
    {
        [Required(ErrorMessage = "Indiquez votre nom"), StringLength(100)]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Indiquez votre téléphone")]
        [RegularExpression(@"^\+?[0-9 .-]{9,15}$", ErrorMessage = "Numéro invalide (ex. 06 12 34 56 78)")]
        public string Phone { get; set; } = "";

        [EmailAddress(ErrorMessage = "Email invalide"), StringLength(150)]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Indiquez l'adresse de livraison"), StringLength(250)]
        public string Address { get; set; } = "";

        [Required(ErrorMessage = "Choisissez une ville")]
        public string City { get; set; } = "Casablanca";

        [Required(ErrorMessage = "Choisissez un jour")]
        public DateTime DeliveryDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Choisissez un créneau")]
        public string DeliverySlot { get; set; } = "";

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public Quote Quote { get; set; } = new();
    public bool LoggedIn { get; set; }

    public List<DateTime> Days { get; } = Enumerable.Range(DateTime.Now.Hour >= 18 ? 1 : 0, 6)
        .Select(i => DateTime.Today.AddDays(i)).ToList();

    /// <summary>Créneaux déjà passés aujourd'hui (non sélectionnables).</summary>
    public static bool SlotPassed(DateTime day, string slot) =>
        day.Date == DateTime.Today && int.TryParse(slot[..2], out var h) && DateTime.Now.Hour >= h - 1;

    public async Task<IActionResult> OnGetAsync()
    {
        Quote = await orders.QuoteAsync(Shop.Lines);
        if (Quote.IsEmpty) return RedirectToPage("/Shop/Cart");

        var id = await Shop.GetCustomerIdAsync();
        LoggedIn = id != null;
        if (id != null && await db.Customers.FindAsync(id.Value) is { } c)
        {
            Input.FullName = c.FullName;
            Input.Phone = c.Phone ?? "";
            Input.Email = c.Email;
            Input.Address = c.Address ?? "";
            Input.City = c.City ?? "Casablanca";
        }
        Input.DeliveryDate = Days[0];
        Input.DeliverySlot = OrderService.DeliverySlots.FirstOrDefault(s => !SlotPassed(Days[0], s)) ?? "";
        if (Input.DeliverySlot == "" && Days.Count > 1) { Input.DeliveryDate = Days[1]; Input.DeliverySlot = OrderService.DeliverySlots[0]; }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Quote = await orders.QuoteAsync(Shop.Lines);
        if (Quote.IsEmpty) return RedirectToPage("/Shop/Cart");
        var customerId = await Shop.GetCustomerIdAsync();
        LoggedIn = customerId != null;

        if (!OrderService.DeliverySlots.Contains(Input.DeliverySlot))
            ModelState.AddModelError("Input.DeliverySlot", "Choisissez un créneau");
        else if (SlotPassed(Input.DeliveryDate, Input.DeliverySlot))
            ModelState.AddModelError("Input.DeliverySlot", "Ce créneau est déjà passé, choisissez-en un autre");
        if (!ModelState.IsValid) return Page();

        var (ok, message, order) = await orders.PlaceAsync(new CheckoutInput
        {
            FullName = Input.FullName, Phone = Input.Phone, Email = Input.Email, Address = Input.Address, City = Input.City,
            DeliveryDate = Input.DeliveryDate, DeliverySlot = Input.DeliverySlot, PaymentMethod = Input.PaymentMethod, Notes = Input.Notes,
        }, Shop.Lines, customerId);

        if (!ok)
        {
            ModelState.AddModelError(string.Empty, message);
            return Page();
        }

        Shop.Clear();
        TempData["last_order"] = order!.Number;
        return RedirectToPage("/Shop/Confirmation", new { number = order.Number });
    }
}
