using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;
using Quiniela.Data.Seeding;
using Quiniela.Web.Components;
using Quiniela.Web.Components.Account;
using Quiniela.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("QuinielaDb")
    ?? throw new InvalidOperationException("Connection string 'QuinielaDb' not found.");

// Registers IDbContextFactory<QuinielaDbContext>. Services must inject the factory and
// create a short-lived context per method call (await using var db = await
// dbFactory.CreateDbContextAsync();) rather than injecting QuinielaDbContext directly.
// Needed for Blazor Server: components like NavMenu render in the same interactive circuit
// as the routed page, and overlapping async calls on a single shared DbContext instance
// throw "A second operation was started on this context instance before a previous
// operation completed."
builder.Services.AddDbContextFactory<QuinielaDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.User.RequireUniqueEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<QuinielaDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/login";
    options.LogoutPath = "/account/logout";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]
            ?? throw new InvalidOperationException("Authentication:Google:ClientId not configured.");
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]
            ?? throw new InvalidOperationException("Authentication:Google:ClientSecret not configured.");
    });

builder.Services.AddScoped<IUserClaimsPrincipalFactory<User>, AppUserClaimsPrincipalFactory>();
builder.Services.AddScoped<PoolService>();
builder.Services.AddScoped<PredictionService>();
builder.Services.AddScoped<ScoringService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<StandingsService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<GroupStandingsService>();
builder.Services.AddScoped<TeamSheetService>();
builder.Services.AddScoped<KnockoutService>();
builder.Services.AddScoped<MatchPredictionsService>();
builder.Services.AddScoped<BracketService>();
builder.Services.AddScoped<PlayerStatsService>();
builder.Services.AddScoped<ChampionService>();
builder.Services.AddScoped<HeadToHeadService>();
builder.Services.AddScoped<AchievementsService>();
builder.Services.AddScoped<PushNotificationService>();
builder.Services.AddScoped<NotificationCheckService>();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/api/logout", async (SignInManager<User> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.LocalRedirect("/login");
}).RequireAuthorization()
  .DisableAntiforgery();

app.MapPost("/api/notify/check", async (
    HttpContext ctx,
    IConfiguration config,
    NotificationCheckService notifSvc) =>
{
    var secret = ctx.Request.Headers["X-Notify-Secret"].ToString();
    if (string.IsNullOrEmpty(secret) || secret != config["Push:NotifySecret"])
    {
        // Evita que UseStatusCodePagesWithReExecute reejecute este 401 como un POST a
        // /not-found (esa reejecución choca con antiforgery y devuelve un 400 confuso).
        ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>()!.Enabled = false;
        return Results.Unauthorized();
    }

    await notifSvc.CheckAndNotifyAsync();
    return Results.Ok();
}).DisableAntiforgery();

app.MapPost("/account/external-login", (
    HttpContext ctx,
    SignInManager<User> signInManager,
    [Microsoft.AspNetCore.Mvc.FromForm] string provider,
    [Microsoft.AspNetCore.Mvc.FromForm] string? returnUrl) =>
{
    var redirectUrl = $"/account/external-login-callback?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}";
    var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
    return Results.Challenge(properties, [provider]);
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var sp = scope.ServiceProvider;
    await sp.GetRequiredService<QuinielaDbContext>().Database.MigrateAsync();
    await DbInitializer.SeedAsync(
        sp.GetRequiredService<QuinielaDbContext>(),
        sp.GetRequiredService<UserManager<User>>(),
        sp.GetRequiredService<RoleManager<IdentityRole<int>>>(),
        app.Configuration,
        sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer"));
}

app.Run();
