using System.Threading.RateLimiting;
using HomeStock.Application;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using HomeStock.Infrastructure;
using HomeStock.Infrastructure.Data;
using HomeStock.Web.Components;
using HomeStock.Web.Components.Account;
using HomeStock.Web.Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ---- Blazor + Razor components ----
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// REST API / service boundary, separate from the Blazor UI.
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// ---- Authentication / Identity ----
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// Data access + application services (provider selected by configuration).
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Per-circuit UI notification bus.
builder.Services.AddScoped<HomeStock.Web.Services.ToastService>();
builder.Services.AddSingleton<HomeStock.Web.Infrastructure.QrCodeService>();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Self-hosted: allow immediate login without an email-confirmation round-trip.
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        // Login rate limiting / brute-force protection.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Identifies the acting user for change-history stamping.
builder.Services.AddScoped<HomeStock.Application.Abstractions.ICurrentUserService, CurrentUserService>();

// ---- Authorization policies (role tiers) ----
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Roles.CanEditPolicy, p => p.RequireRole(Roles.Administrator, Roles.StandardUser))
    .AddPolicy(Roles.AdminPolicy, p => p.RequireRole(Roles.Administrator));

// ---- Secure cookies ----
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // SameAsRequest keeps plain-HTTP LAN access working while enforcing Secure over HTTPS.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// ---- Reverse-proxy / forwarded headers (for future remote access) ----
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trusted proxies come from configuration; defaults clear the known lists so only
    // configured proxies are honoured (avoids spoofed forwarded headers).
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        if (System.Net.IPAddress.TryParse(proxy, out var ip)) options.KnownProxies.Add(ip);
});

// ---- Rate limiting (login brute-force mitigation) ----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// ---- Health checks (app, database, attachment storage) ----
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database")
    .AddCheck<StorageHealthCheck>("attachment-storage");

var app = builder.Build();

// Honour proxy headers before anything else in the pipeline.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Only redirect to HTTPS when a TLS endpoint is actually configured; on a plain-HTTP LAN
// install an unconditional redirect would make the app unreachable.
if (builder.Configuration.GetValue("EnableHttpsRedirection", false))
    app.UseHttpsRedirection();

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();
app.MapControllers();

// Liveness (process up) and readiness (DB + storage) probes.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health"); // aggregate

// ---- Apply migrations + seed on startup ----
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
