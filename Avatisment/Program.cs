using Avatisment.Controllers;
using Avatisment.Services;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// In-memory data store that seeds demo content for the feed.
// Swap this for a real database (EF Core + SQL Server/PostgreSQL) in production.
builder.Services.AddSingleton<IAvatismentDataStore, InMemoryDataStore>();

// Gate: signed-in users must finish email + phone verification before reaching any page.
builder.Services.AddScoped<RequireVerifiedAccountFilter>();

// Cookie auth, hardened: HttpOnly (no JS access), Secure in non-dev environments,
// SameSite=Lax (blocks most cross-site submission while still allowing normal navigation).
builder.Services.AddAuthentication("AvatismentAuth")
    .AddCookie("AvatismentAuth", options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });

// Antiforgery cookie hardening (CSRF protection is already applied per-action via
// [ValidateAntiForgeryToken]; this just locks down the token cookie itself).
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Baseline hardening headers on every response.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Lightweight health check for container orchestrators / PaaS uptime probes.
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timeUtc = DateTime.UtcNow }))
    .AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
