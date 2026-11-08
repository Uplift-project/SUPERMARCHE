using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Admin.Users;

[HasPermission(Permissions.UsersManage)]
public class EditModel(AppDbContext db, AccountService accounts) : AppPageModel
{
    public class UserInput
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Le nom est obligatoire"), StringLength(100)]
        [Display(Name = "Nom complet")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "L'email est obligatoire"), EmailAddress(ErrorMessage = "Email invalide"), StringLength(150)]
        [Display(Name = "Email (identifiant de connexion)")]
        public string Email { get; set; } = string.Empty;

        [StringLength(30), Display(Name = "Téléphone")]
        public string? Phone { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Choisissez un rôle"), Display(Name = "Rôle")]
        public int RoleId { get; set; }

        [Display(Name = "Compte actif")]
        public bool IsActive { get; set; } = true;

        [StringLength(100, MinimumLength = 6, ErrorMessage = "Au moins 6 caractères"), DataType(DataType.Password)]
        [Display(Name = "Mot de passe")]
        public string? Password { get; set; }
    }

    [BindProperty] public UserInput Input { get; set; } = new();
    public List<Role> Roles { get; set; } = [];
    public bool IsNew => Input.Id == 0;
    public bool IsMe => Input.Id == Access.User?.Id;

    private async Task LoadAsync()
    {
        var q = db.Roles.AsNoTracking().OrderBy(r => r.Name).AsQueryable();
        // Seul un Super Admin peut attribuer le rôle Super Admin.
        if (!Access.IsSuperAdmin) q = q.Where(r => !r.IsSuperAdmin);
        Roles = await q.ToListAsync();
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is > 0)
        {
            var u = await db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == id);
            if (u == null) return NotFound();
            if (u.Role!.IsSuperAdmin && !Access.IsSuperAdmin) return Forbid();
            Input = new UserInput { Id = u.Id, FullName = u.FullName, Email = u.Email, Phone = u.Phone, RoleId = u.RoleId, IsActive = u.IsActive };
        }
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        Input.Email = Input.Email.Trim().ToLowerInvariant();

        if (IsNew && string.IsNullOrWhiteSpace(Input.Password))
            ModelState.AddModelError("Input.Password", "Le mot de passe est obligatoire pour un nouveau compte.");
        if (await db.Users.AnyAsync(u => u.Email == Input.Email && u.Id != Input.Id))
            ModelState.AddModelError("Input.Email", "Cet email est déjà utilisé par un autre employé.");
        if (Roles.All(r => r.Id != Input.RoleId))
            ModelState.AddModelError("Input.RoleId", "Rôle non autorisé.");

        if (!ModelState.IsValid) return Page();

        if (IsNew)
        {
            var user = new AppUser
            {
                FullName = Input.FullName.Trim(), Email = Input.Email, Phone = Input.Phone,
                RoleId = Input.RoleId, IsActive = Input.IsActive, CreatedAt = DateTime.Now,
            };
            user.PasswordHash = AccountService.HashPassword(user, Input.Password!);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            Success($"Compte créé pour {user.FullName}. Identifiant : {user.Email}");
            return RedirectToPage("Details", new { id = user.Id });
        }

        var existing = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == Input.Id);
        if (existing == null) return NotFound();
        if (existing.Role!.IsSuperAdmin && !Access.IsSuperAdmin) return Forbid();

        if (IsMe && (existing.RoleId != Input.RoleId || !Input.IsActive))
        {
            ModelState.AddModelError(string.Empty, "Vous ne pouvez pas changer votre propre rôle ni désactiver votre compte.");
            return Page();
        }

        existing.FullName = Input.FullName.Trim();
        existing.Email = Input.Email;
        existing.Phone = Input.Phone;
        existing.RoleId = Input.RoleId;
        existing.IsActive = Input.IsActive;
        await db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(Input.Password))
            await accounts.SetPasswordAsync(existing, Input.Password,
                $"Mot de passe de {existing.FullName} réinitialisé par {Access.User!.FullName}");

        Success($"Compte de {existing.FullName} mis à jour.");
        return RedirectToPage("Details", new { id = existing.Id });
    }
}
