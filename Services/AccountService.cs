using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Models;
using Services.Data;

namespace Services;

public class AccountService(AppDbContext db)
{
    private static readonly PasswordHasher<AppUser> Hasher = new();

    public static string HashPassword(AppUser user, string password) => Hasher.HashPassword(user, password);

    public static bool VerifyPassword(AppUser user, string password) =>
        !string.IsNullOrEmpty(user.PasswordHash) &&
        Hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;

    /// <summary>Vérifie email + mot de passe. Journalise les succès et les échecs.</summary>
    public async Task<AppUser?> LoginAsync(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !user.IsActive || !VerifyPassword(user, password))
        {
            db.AddAudit(AuditActions.LoginFailed, "Compte", user?.Id,
                user is { IsActive: false } ? $"Tentative de connexion sur un compte désactivé ({email})"
                                            : $"Échec de connexion pour {email}",
                userId: user?.Id, userName: user?.FullName ?? email);
            await db.SaveChangesAsync();
            return null;
        }

        user.LastLoginAt = DateTime.Now;
        db.AddAudit(AuditActions.Login, "Compte", user.Id, $"{user.FullName} s'est connecté(e)",
            userId: user.Id, userName: user.FullName);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task LogoutAsync(int userId, string name)
    {
        db.AddAudit(AuditActions.Logout, "Compte", userId, $"{name} s'est déconnecté(e)", userId: userId, userName: name);
        await db.SaveChangesAsync();
    }

    /// <summary>Définit un nouveau mot de passe et invalide les sessions existantes.</summary>
    public async Task SetPasswordAsync(AppUser user, string newPassword, string description)
    {
        user.PasswordHash = HashPassword(user, newPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        db.AddAudit(AuditActions.PasswordReset, "Employé", user.Id, description);
        await db.SaveChangesAsync();
    }
}
