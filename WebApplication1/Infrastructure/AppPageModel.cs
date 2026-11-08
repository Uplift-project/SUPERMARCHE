using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Services;

namespace WebApplication1.Infrastructure;

/// <summary>Page de base : donne accès aux droits de l'employé connecté + messages flash.</summary>
public abstract class AppPageModel : PageModel
{
    public UserAccess Access { get; private set; } = new();

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        Access = await HttpContext.RequestServices.GetRequiredService<UserAccessService>().GetAsync();
        await next();
    }

    public bool Can(string permission) => Access.Can(permission);

    protected void Success(string message) => TempData["success"] = message;
    protected void Error(string message) => TempData["error"] = message;

    /// <summary>Renvoie 403 si l'employé n'a pas la permission (pour les actions d'écriture).</summary>
    protected IActionResult? Require(string permission) => Can(permission) ? null : Forbid();
}

public class PagedList<T>
{
    public const int DefaultPageSize = 25;
    public List<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int Total { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public static class PagingExtensions
{
    public static async Task<PagedList<T>> ToPagedAsync<T>(this IQueryable<T> query, int page, int pageSize = PagedList<T>.DefaultPageSize)
    {
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedList<T> { Items = items, Page = page, PageSize = pageSize, Total = total };
    }
}

public record PagerInfo(int Page, int TotalPages, int Total);
