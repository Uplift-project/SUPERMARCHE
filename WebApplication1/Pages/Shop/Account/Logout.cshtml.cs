using Microsoft.AspNetCore.Mvc;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Shop.Account;

public class LogoutModel : ShopPageModel
{
    public IActionResult OnGet() => RedirectToPage("/Shop/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        await Shop.SignOutAsync();
        Flash("Vous êtes déconnecté(e). À bientôt !");
        return RedirectToPage("/Shop/Index");
    }
}
