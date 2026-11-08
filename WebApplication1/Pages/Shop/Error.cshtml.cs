using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebApplication1.Pages.Shop;

/// <summary>Page affichée en cas d'erreur serveur (500) ou de page introuvable (404).</summary>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public int Code { get; set; }
    public string RequestId { get; set; } = "";

    public void OnGet(int? code) => Fill(code);
    public void OnPost(int? code) => Fill(code);

    private void Fill(int? code)
    {
        Code = code ?? 500;
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        Response.StatusCode = Code;
    }
}
