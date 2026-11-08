using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Services;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Shop.Account;

public class LoginModel(ShopAccountService accounts) : ShopPageModel
{
    [BindProperty, Required(ErrorMessage = "Indiquez votre email"), EmailAddress(ErrorMessage = "Email invalide")]
    public string Email { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Indiquez votre mot de passe"), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [BindProperty] public bool RememberMe { get; set; } = true;
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync() =>
        await Shop.GetCustomerIdAsync() != null ? RedirectToPage("/Shop/Account/Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var c = await accounts.LoginAsync(Email, Password);
        if (c == null)
        {
            ModelState.AddModelError(string.Empty, "Email ou mot de passe incorrect.");
            return Page();
        }
        await Shop.SignInAsync(c, RememberMe);
        Flash($"Bienvenue {c.FullName.Split(' ')[0]} !");
        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : RedirectToPage("/Shop/Account/Index");
    }
}
