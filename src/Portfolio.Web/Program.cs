using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Portfolio.Web.Api;
using Portfolio.Web.Auth;
using Portfolio.Web.Components;
using Portfolio.Web.Data;
using Portfolio.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database (MySQL + EF Core) ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");
// A fixed server version lets the app (and `dotnet ef`) start even when MySQL is offline.
var mySqlVersion = new MySqlServerVersion(Version.Parse(builder.Configuration["MySql:ServerVersion"] ?? "8.0.36"));

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseMySql(connectionString, mySqlVersion, mysql => mysql.EnableRetryOnFailure(3)));

// ---------- JWT authentication ----------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key deve ter pelo menos 32 caracteres.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwt.ValidationParameters;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.ApiAdmin, policy => policy
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireRole(TokenService.AdminRole));

builder.Services.AddMemoryCache();
builder.Services.AddScoped<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddCascadingAuthenticationState();

// ---------- Rate limiting (login + contact form) ----------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(5) }));
    options.AddPolicy("contact", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10) }));
});

// ---------- App services ----------
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<ProfileOptions>(builder.Configuration.GetSection("Profile"));
builder.Services.AddScoped<ImageStorageService>();
builder.Services.AddScoped<PortfolioService>();
builder.Services.AddScoped<ContactService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment())
    // Allow image uploads up to 5 MB through the SignalR circuit.
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 6 * 1024 * 1024);

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// Uploaded images live in wwwroot/uploads (created at runtime, so not part of MapStaticAssets).
app.UseStaticFiles();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapPortfolioApi();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
