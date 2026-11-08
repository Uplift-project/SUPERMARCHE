using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Account;

public class ProfileModel(AppDbContext db, AccountService accounts) : AppPageModel
{
    public AppUser Me { get; set; } = null!;
    public List<AuditLog> MyActivity { get; set; } = [];

    [BindProperty, Required(ErrorMessage = "Obligatoire"), DataType(DataType.Password)]
    [Display(Name = "Mot de passe actuel")]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty, Required(ErrorMessage = "Obligatoire"), DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Au moins 6 caractères")]
    [Display(Name = "Nouveau mot de passe")]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Les mots de passe ne correspondent pas")]
    [Display(Name = "Confirmer")]
    public string ConfirmPassword { get; set; } = string.Empty;

    private async Task LoadAsync()
    {
        var id = Access.User!.Id;
        Me = await db.Users.Include(u => u.Role).FirstAsync(u => u.Id == id);
        MyActivity = await db.AuditLogs.Where(a => a.UserId == id)
            .OrderByDescending(a => a.Id).Take(15).ToListAsync();
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (!ModelState.IsValid) return Page();

        if (!AccountService.VerifyPassword(Me, CurrentPassword))
        {
            ModelState.AddModelError(nameof(CurrentPassword), "Mot de passe actuel incorrect");
            return Page();
        }

        await accounts.SetPasswordAsync(Me, NewPassword, $"{Me.FullName} a changé son mot de passe");

        // Le "security stamp" a changé : on renouvelle le cookie pour rester connecté.
        var identity = (ClaimsIdentity)User.Identity!;
        identity.RemoveClaim(identity.FindFirst("stamp"));
        identity.AddClaim(new Claim("stamp", Me.SecurityStamp));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        Success("Votre mot de passe a été modifié.");
        return RedirectToPage();
    }
}
