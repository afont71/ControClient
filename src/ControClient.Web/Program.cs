using System.Security.Claims;
using ControClient.Infrastructure;
using ControClient.Web.Components;
using ControClient.Web.Endpoints;
using ControClient.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Evita el proveedor EventLog de Windows (requiere permisos en el registro de
// eventos) y deja logging explícito: consola en desarrollo, servicio/archivo en producción.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddOpenApi();

// La raíz del almacén documental se resuelve aquí. Por defecto va a datos locales
// del usuario: nunca dentro del repositorio. En producción se configura absoluta.
var documentsRoot = builder.Configuration["Storage:DocumentsRoot"];
if (string.IsNullOrWhiteSpace(documentsRoot))
    documentsRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ControClient", "documents");
else if (!Path.IsPathRooted(documentsRoot))
    documentsRoot = Path.Combine(builder.Environment.ContentRootPath, documentsRoot);
builder.Configuration["Storage:DocumentsRoot"] = documentsRoot;

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ControClient.Web.Pages.ClientPageService>();
builder.Services.AddScoped<ControClient.Web.Pages.FormPageService>();
builder.Services.AddScoped<ControClient.Web.Pages.DocumentPageService>();
builder.Services.AddScoped<ControClient.Web.Pages.VaultPageService>();
builder.Services.AddScoped<ControClient.Web.Pages.SearchPageService>();

var authenticationMode = builder.Configuration["Authentication:Mode"] ?? "Entra";
var useDevAuth = builder.Environment.IsDevelopment()
                 && authenticationMode.Equals("Development", StringComparison.OrdinalIgnoreCase);

// Fail-fast: en producción con Entra no se arranca con valores de plantilla.
if (!builder.Environment.IsDevelopment() && !useDevAuth)
{
    static bool IsPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("PENDIENTE", StringComparison.OrdinalIgnoreCase);

    if (IsPlaceholder(builder.Configuration["AzureAd:TenantId"])
        || IsPlaceholder(builder.Configuration["AzureAd:ClientId"]))
    {
        throw new InvalidOperationException(
            "Configuración incompleta: define AzureAd:TenantId y AzureAd:ClientId (user-secrets o variables de entorno) " +
            "antes de arrancar en producción. Ver docs/entra-id-setup.md.");
    }
}

var authBuilder = builder.Services.AddAuthentication();

if (useDevAuth)
{
    authBuilder.AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(
        DevAuthenticationHandler.SchemeName, _ => { });
    authBuilder.AddPolicyScheme("CookieOrBearer", "Development", options =>
        options.ForwardDefaultSelector = _ => DevAuthenticationHandler.SchemeName);
}
else
{
    // Web: cookie de Entra ID; API: JWT Bearer del mismo tenant (SEC-01, SEC-03).
    authBuilder.AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
    authBuilder.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"), JwtBearerDefaults.AuthenticationScheme);

    authBuilder.AddPolicyScheme("CookieOrBearer", "Cookie or Bearer", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.Authorization.Count > 0
                ? JwtBearerDefaults.AuthenticationScheme
                : CookieAuthenticationDefaults.AuthenticationScheme;
    });
}

builder.Services.Configure<AuthenticationOptions>(options =>
{
    options.DefaultScheme = "CookieOrBearer";
    options.DefaultChallengeScheme = "CookieOrBearer";
});

var requireMfaClaim = builder.Configuration.GetValue("Authentication:RequireMfaClaim", true);

builder.Services.AddAuthorization(options =>
{
    // SEC-02: la MFA se exige en el tenant. La comprobación del claim es una segunda
    // barrera y puede desactivarse si el tenant no emite "amr".
    options.AddPolicy("RequireMfa", policy =>
    {
        policy.RequireAuthenticatedUser();
        if (requireMfaClaim)
        {
            policy.RequireAssertion(context =>
                context.User.HasClaim("amr", "mfa")
                || context.User.FindAll("acrs").Any(c => c.Value.Contains("mfa", StringComparison.OrdinalIgnoreCase)));
        }
    });

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim(ClaimTypes.Role, "Administrator"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapSystemEndpoints();
app.MapClientEndpoints();
app.MapFormEndpoints();
app.MapDocumentEndpoints();
 app.MapVaultEndpoints();
app.MapSearchEndpoints();
app.MapOpenApi();

// Plantillas iniciales (idempotente). En producción puede desactivarse por configuración.
if (app.Configuration.GetValue("Seeding:DefaultTemplates", true))
{
    using var seedScope = app.Services.CreateScope();
    await ControClient.Infrastructure.Forms.DefaultTemplateSeeder.SeedAsync(
        seedScope.ServiceProvider.GetRequiredService<ControClient.Infrastructure.Data.AppDbContext>());
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;

