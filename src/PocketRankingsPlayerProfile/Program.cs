using Microsoft.AspNetCore.Authentication.Cookies;
using Npgsql;
using PocketRankingsPlayerProfile.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/development/access";
        options.AccessDeniedPath = "/development/access-denied";
        options.Cookie.Name = "PocketRankings.PlayerProfile.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    });
builder.Services.AddAuthorization(options =>
    options.AddPolicy("ProfileManager", policy => policy.RequireRole("Owner", "ProfileAdmin", "Player")));
var connectionString = builder.Configuration.GetConnectionString("PlayerProfile");
var privacyKey = builder.Configuration["Privacy:SuppressionHashKey"];
var accountPublicKey = builder.Configuration["AccountIdentity:PublicKeyPem"];
var installationKey = builder.Configuration["Installation:Key"];
var privacyReceiverEnabled = builder.Configuration.GetValue<bool>("PrivacyReceiver:Enabled");
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(privacyKey))
    throw new InvalidOperationException("Privacy:SuppressionHashKey is required outside Development.");
if (privacyReceiverEnabled && (string.IsNullOrWhiteSpace(accountPublicKey) || string.IsNullOrWhiteSpace(installationKey)))
    throw new InvalidOperationException("AccountIdentity:PublicKeyPem and Installation:Key are required when the privacy receiver is enabled.");
builder.Services.AddSingleton(new PrivacySuppressionHasher(privacyKey ?? "development-only-player-profile-privacy-key"));
builder.Services.AddSingleton<PrivacyDirectiveVerifier>();
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
    builder.Services.AddSingleton<IPlayerProfileStore, PostgresPlayerProfileStore>();
    builder.Services.AddSingleton<PostgresSchemaInitializer>();
}
else
{
    if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("ConnectionStrings:PlayerProfile is required outside Development.");
    builder.Services.AddSingleton<IPlayerProfileStore, DevelopmentPlayerProfileStore>();
}

var app = builder.Build();
if (app.Services.GetService<PostgresSchemaInitializer>() is { } schema) await schema.InitializeAsync(app.Lifetime.ApplicationStopping);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/home/error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; img-src 'self' data: https:; style-src 'self'; script-src 'self' https://platform.twitter.com; frame-src https://www.facebook.com https://platform.twitter.com https://syndication.twitter.com; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "profile", pattern: "players/{slug}", defaults: new { controller = "Players", action = "Details" });
app.MapControllerRoute(name: "default", pattern: "{controller=Players}/{action=Index}/{id?}");
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready", store = string.IsNullOrWhiteSpace(connectionString) ? "development" : "postgresql" }));

app.Run();

public partial class Program;
