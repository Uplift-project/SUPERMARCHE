using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Services;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Shop.Account;

public class RegisterModel(ShopAccountService accounts) : ShopPageModel
{
    public class InputModel
    {
        [Required(ErrorMessage = "Indiquez votre nom"), StringLength(100)]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Indiquez votre email"), EmailAddress(ErrorMessage = "Email invalide"), StringLength(150)]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Indiquez votre téléphone")]
        [RegularExpression(@"^\+?[0-9 .-]{9,15}$", ErrorMessage = "Numéro invalide (ex. 06 12 34 56 78)")]
        public string Phone { get; set; } = "";

        [Required(ErrorMessage = "Choisissez un mot de passe"), MinLength(6, ErrorMessage = "6 caractères minimum"), DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas"), DataType(DataType.Password)]
        public string Confirm { get; set; } = "";
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var (ok, message, customer) = await accounts.RegisterAsync(Input.FullName, Input.Email, Input.Phone, Input.Password);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, message);
            return Page();
        }
        await Shop.SignInAsync(customer!, true);
        Flash($"Bienvenue {customer!.FullName.Split(' ')[0]}, votre compte est créé !");
        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : RedirectToPage("/Shop/Index");
    }
}
