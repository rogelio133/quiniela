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

builder.Services.AddDbContext<QuinielaDbContext>(options =>
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

builder.Services.AddScoped<IUserClaimsPrincipalFactory<User>, AppUserClaimsPrincipalFactory>();
builder.Services.AddScoped<PoolService>();
builder.Services.AddScoped<PredictionService>();

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

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await using (var scope = app.Services.CreateAsyncScope())
{
    var sp = scope.ServiceProvider;
    await DbInitializer.SeedAsync(
        sp.GetRequiredService<QuinielaDbContext>(),
        sp.GetRequiredService<UserManager<User>>(),
        sp.GetRequiredService<RoleManager<IdentityRole<int>>>(),
        app.Configuration,
        sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer"));
}

app.Run();
