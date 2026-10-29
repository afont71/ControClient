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
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ControClient.Web.Pages.ClientPageService>();

var authenticationMode = builder.Configuration["Authentication:Mode"] ?? "Entra";
var useDevAuth = builder.Environment.IsDevelopment()
                 && authenticationMode.Equals("Development", StringComparison.OrdinalIgnoreCase);

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;

