using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Services;

/// <summary>L'employé connecté qui fait la requête en cours.</summary>
public interface ICurrentUser
{
    int? Id { get; }
    string Name { get; }
    string? IpAddress { get; }
}

public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public int? Id => int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string Name => Principal?.Identity?.IsAuthenticated == true
        ? Principal.FindFirstValue(ClaimTypes.Name) ?? "Inconnu"
        : "Système";

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
