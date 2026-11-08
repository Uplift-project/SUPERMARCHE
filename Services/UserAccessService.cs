using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;

namespace Services;

/// <summary>Droits de l'employé connecté (chargés une seule fois par requête).</summary>
public class UserAccess
{
    public AppUser? User { get; init; }
    public bool IsSuperAdmin { get; init; }
    public HashSet<string> Permissions { get; init; } = [];

    public bool IsAuthenticated => User != null;
    public bool Can(string permission) => IsSuperAdmin || Permissions.Contains(permission);
    public bool CanAny(params string[] permissions) => permissions.Any(Can);
}

public class UserAccessService(AppDbContext db, ICurrentUser currentUser)
{
    private UserAccess? _cached;

    public async Task<UserAccess> GetAsync()
    {
        if (_cached != null) return _cached;

        var id = currentUser.Id;
        if (id == null) return _cached = new UserAccess();

        var user = await db.Users.AsNoTracking().Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
        if (user?.Role == null) return _cached = new UserAccess();

        return _cached = new UserAccess
        {
            User = user,
            IsSuperAdmin = user.Role.IsSuperAdmin,
            Permissions = user.Role.PermissionSet,
        };
    }

    public async Task<bool> CanAsync(string permission) => (await GetAsync()).Can(permission);
}
