using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Services;
using Services.Data;
using WebApplication1.Infrastructure;

// Culture française, mais avec le point comme séparateur décimal (compatible avec <input type="number">).
var culture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.GetCultureInfo("fr-FR").Clone();
culture.NumberFormat.NumberDecimalSeparator = ".";
culture.NumberFormat.CurrencyDecimalSeparator = ".";
culture.NumberFormat.NumberGroupSeparator = " ";
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebApplication.CreateBuilder(args);

// ---------- Base de données (SQL Server — LocalDB en développement) ----------
// Chaîne de connexion dans appsettings.json → "ConnectionStrings:Default".
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Chaîne de connexion « Default » manquante dans appsettings.json")));

// ---------- Services métier ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<UserAccessService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<SaleService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ShopAccountService>();
builder.Services.AddScoped<ShopSession>();

// Clés de chiffrement des cookies (connexion, panier, formulaires) enregistrées sur disque :
// sans ça, chez un hébergeur, chaque redémarrage du site déconnecte tout le monde.
builder.Services.AddDataProtection()
    .SetApplicationName("SuperMarche")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));

// ---------- Authentification par cookie ----------
// Deux espaces séparés : les employés (admin, schéma par défaut) et les clients de la boutique.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(ShopSession.Scheme, o =>
    {
        o.LoginPath = "/compte/connexion";
        o.LogoutPath = "/compte/deconnexion";
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        o.Cookie.Name = "Supermarche.Client";
        o.Cookie.HttpOnly = true;
    })
    .AddCookie(o =>
    {
        o.LoginPath = "/admin/account/login";
        o.LogoutPath = "/admin/account/logout";
        o.AccessDeniedPath = "/admin/account/accessdenied";
        o.ExpireTimeSpan = TimeSpan.FromHours(10);
        o.SlidingExpiration = true;
        o.Cookie.Name = "Supermarche.Auth";
        o.Cookie.HttpOnly = true;
        o.Events.OnValidatePrincipal = async ctx =>
        {
            // Compte désactivé, supprimé ou mot de passe changé → déconnexion immédiate.
            var id = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var stamp = ctx.Principal?.FindFirstValue("stamp");
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var ok = int.TryParse(id, out var userId) &&
                     await db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.SecurityStamp == stamp);
            if (!ok)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// ---------- Autorisation par permission ----------
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
// Pas de FallbackPolicy globale : elle redirigeait aussi les adresses inconnues de la boutique vers
// la connexion admin. La protection est posée sur les pages Razor (voir AuthorizeFolder plus bas).
builder.Services.AddAuthorization();

builder.Services.Configure<RouteOptions>(o => o.LowercaseUrls = true);
// Garde les accents et emojis lisibles dans le HTML (au lieu de &#xE9;…).
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(o =>
    o.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));
builder.Services.AddRazorPages(o =>
{
    // Toutes les pages exigent une connexion employé, sauf [AllowAnonymous] (connexion admin)…
    o.Conventions.AuthorizeFolder("/");
    // …et la boutique (Pages/Shop), publique et à la racine du site : /, /c/{rayon}, /panier…
    o.Conventions.AllowAnonymousToFolder("/Shop");
    // Tout le reste est l'espace d'administration, servi sous /admin/…
    o.Conventions.AddFolderRouteModelConvention("/", model =>
    {
        if (model.ViewEnginePath.StartsWith("/Shop/", StringComparison.OrdinalIgnoreCase)) return;
        foreach (var selector in model.Selectors)
        {
            var route = selector.AttributeRouteModel;
            if (route?.Template == null || route.Template.StartsWith("admin", StringComparison.OrdinalIgnoreCase)) continue;
            route.Template = route.Template.Length == 0 ? "admin" : "admin/" + route.Template;
        }
    });
});

var app = builder.Build();

// Création + remplissage de la base au premier lancement
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/erreur");
    app.UseHsts();
}
// Page « introuvable » (404) et autres codes d'erreur, en français et au design de la boutique.
app.UseStatusCodePagesWithReExecute("/erreur/{0}");

app.UseHttpsRedirection();
// Photos des produits envoyées depuis l'admin (wwwroot/uploads), servies sans connexion.
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Les fichiers CSS/JS/images doivent rester accessibles sans connexion (sinon la FallbackPolicy les redirige vers /login).
app.MapStaticAssets().AllowAnonymous();
app.MapRazorPages().WithStaticAssets();

app.Run();
