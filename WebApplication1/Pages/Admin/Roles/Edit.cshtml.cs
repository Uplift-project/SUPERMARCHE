using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;
using WebApplication1.Infrastructure;

namespace WebApplication1.Pages.Admin.Roles;

[HasPermission(Permissions.RolesManage)]
public class EditModel(AppDbContext db) : AppPageModel
{
    [BindProperty] public Role Role { get; set; } = new();
    [BindProperty] public List<string> Selected { get; set; } = [];
    public bool IsNew => Role.Id == 0;

    /// <summary>Un non-Super Admin ne peut pas donner des droits qu'il n'a pas lui-même.</summary>
    public bool CanGrant(string permission) => Access.IsSuperAdmin || Access.Permissions.Contains(permission);

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is > 0)
        {
            var r = await db.Roles.FindAsync(id.Value);
            if (r == null) return NotFound();
            if (r.IsSuperAdmin) return RedirectToPage("Index");
            Role = r;
            Selected = r.PermissionSet.ToList();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Remove("Role.Users");
        Role.Name = Role.Name?.Trim() ?? "";
        if (await db.Roles.AnyAsync(r => r.Name == Role.Name && r.Id != Role.Id))
            ModelState.AddModelError("Role.Name", "Un rôle avec ce nom existe déjà.");
        if (!ModelState.IsValid) return Page();

        var valid = Permissions.All.ToHashSet();
        Role? existing = null;
        if (!IsNew)
        {
            existing = await db.Roles.FindAsync(Role.Id);
            if (existing == null) return NotFound();
            if (existing.IsSuperAdmin) return Forbid();
        }

        // Permissions demandées (filtrées) ; on conserve celles que l'utilisateur ne peut pas accorder.
        var previous = existing?.PermissionSet ?? [];
        var result = Selected.Where(valid.Contains).Where(CanGrant).ToHashSet();
        result.UnionWith(previous.Where(p => !CanGrant(p)));

        if (existing == null)
        {
            db.Roles.Add(new Role { Name = Role.Name, Description = Role.Description, PermissionSet = result });
        }
        else
        {
            existing.Name = Role.Name;
            existing.Description = Role.Description;
            existing.PermissionSet = result;
        }
        await db.SaveChangesAsync();
        Success($"Rôle « {Role.Name} » enregistré. Les accès s'appliquent immédiatement aux employés concernés.");
        return RedirectToPage("Index");
    }
}
