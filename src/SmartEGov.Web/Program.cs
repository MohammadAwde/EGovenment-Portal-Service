using Microsoft.AspNetCore.Identity;
using SmartEGov.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using SmartEGov.Infrastructure;
using SmartEGov.Infrastructure.Data;
using SmartEGov.Infrastructure.Authorization;
using SmartEGov.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.IO;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using System.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;


var builder = WebApplication.CreateBuilder(args);

// Add Infrastructure services (DbContext, repositories, services)
builder.Services.AddInfrastructure(builder.Configuration);

Console.WriteLine(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

// Add Identity with enhanced security settings
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password policy
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 4;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Add external authentication providers (Google, Apple)
// Configure only when configuration values are present to allow optional setup.
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle("Google", options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.SaveTokens = true;
        });
}

// Apple Sign In requires additional setup (private key and client secret). Add when ready using AspNet.Security.OAuth.Apple

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    // Allow cross-scheme usage when the site is fully HTTPS; set to None so SameSite does not block cross-scheme scenarios
    options.Cookie.SameSite = SameSiteMode.None;
});

// Add Authorization with custom policies
builder.Services.AddAuthorization(options =>
{
    options.AddAuthorizationPolicies();
});

// Add Antiforgery with enhanced settings
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    // Allow the antiforgery cookie to be sent when using HTTPS and cross-site contexts (SameSite=None required)
    options.Cookie.SameSite = SameSiteMode.None;
});

// Add HSTS
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(SmartEGov.Web.Resources.SharedResource));
    });
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Blazor Server removed to avoid websocket/hotreload injection issues in development.
// If you need Blazor in the future, re-enable AddServerSideBlazor() and MapBlazorHub().

// Health checks (for /health endpoint) and response compression
builder.Services.AddHealthChecks();

// Response compression to improve throughput
// Configure response compression. Note: exclude HTML from compression so middleware
// that injects scripts into HTML responses (BrowserLink / Hot Reload) can operate
// even when compression providers are enabled.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();

    // In development exclude text/html so BrowserLink / Hot Reload can inject
    // scripts into HTML responses. In production keep default MIME types so
    // HTML can still be compressed.
    if (builder.Environment.IsDevelopment())
    {
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes
            .Where(m => !string.Equals(m, "text/html", StringComparison.OrdinalIgnoreCase))
            .Concat(new[] { "application/json" });
    }
    else
    {
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/json" });
    }
});

// When the app runs behind a reverse proxy (nginx/IIS/ingress) enable forwarded
// headers so authentication, HTTPS detection and websocket upgrades work correctly.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Clear the default known networks so that forwarded headers are accepted
    // when running inside some docker or cloud environments. Keep this secure
    // in production by restricting KnownNetworks/KnownProxies if needed.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opt => opt.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(opt => opt.Level = System.IO.Compression.CompressionLevel.Fastest);

// Caching (in-memory). For multi-instance use a distributed cache like Redis.
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSignalR();

var app = builder.Build();

// Seed roles and default Admin user
try
{
    using (var scope = app.Services.CreateScope())
    {
        // Ensure database schema is up-to-date by applying any pending migrations
        try
        {
            var dbForMigrations = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbForMigrations.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup:Migrations");
            logger.LogWarning(ex, "Automatic database migration failed. Continue startup.");
        }
        // Ensure Stripe columns exist in Payments table when running in Development
        try
        {
            var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
            if (env.IsDevelopment())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup:StripeColumns");
                var appDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var conn = appDb.Database.GetDbConnection();
                await conn.OpenAsync();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Payments' AND COLUMN_NAME = 'StripePaymentIntentId'";
                    var result = await cmd.ExecuteScalarAsync();
                    var hasPi = Convert.ToInt32(result) > 0;

                    cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Payments' AND COLUMN_NAME = 'StripeSessionId'";
                    result = await cmd.ExecuteScalarAsync();
                    var hasSession = Convert.ToInt32(result) > 0;

                    if (!hasPi)
                    {
                        logger.LogInformation("Adding missing column StripePaymentIntentId to Payments table (development automatic fix).");
                        using var a = conn.CreateCommand();
                        a.CommandText = "ALTER TABLE Payments ADD StripePaymentIntentId nvarchar(max) NULL;";
                        await a.ExecuteNonQueryAsync();
                    }

                    if (!hasSession)
                    {
                        logger.LogInformation("Adding missing column StripeSessionId to Payments table (development automatic fix).");
                        using var b = conn.CreateCommand();
                        b.CommandText = "ALTER TABLE Payments ADD StripeSessionId nvarchar(max) NULL;";
                        await b.ExecuteNonQueryAsync();
                    }
                }
                finally
                {
                    await conn.CloseAsync();
                }
            }
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup:StripeColumns");
            logger.LogWarning(ex, "Automatic check/creation of Stripe columns failed. Continue startup.");
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        string[] roles = { "Admin", "Officer", "Citizen" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Seed default Admin user
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var adminConfig = app.Configuration.GetSection("DefaultAdmin");
        var adminEmail = adminConfig["Email"] ?? "admin@smartegov.com";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var adminUser = new ApplicationUser
            {
                FullName = adminConfig["FullName"] ?? "System Administrator",
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(adminUser, adminConfig["Password"] ?? "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // Seed Lebanese government departments, services, workflows, and required documents
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await LebaneseSeedData.SeedAsync(dbContext);
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");
    logger.LogError(ex, "An error occurred while seeding the database. The application will continue without seeding.");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Security headers middleware
app.UseMiddleware<SecurityHeadersMiddleware>();

// Enforce cookie policy so SameSite=None and Secure cookies are respected
app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.None,
    Secure = CookieSecurePolicy.Always
});

app.UseHttpsRedirection();
// Compress responses to reduce bandwidth and speed up responses under load
app.UseResponseCompression();
app.UseStaticFiles();
app.UseRouting();

// Ensure forwarded headers are processed before authentication so schemes
// and HTTPS detection work correctly when behind a proxy.
app.UseForwardedHeaders();

// Enable WebSockets explicitly to ensure the server accepts websocket
// upgrade requests used by Blazor Server and hot-reload tools.
app.UseWebSockets();

app.UseAuthentication();
app.UseAuthorization();
app.MapHub<SmartEGov.Web.Hubs.SupportHub>("/supportHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Blazor server hub removed. Server-side Blazor has been disabled to avoid
// reconnect and script-injection problems in development environments.

// Health endpoint for load balancers / orchestrators
app.MapHealthChecks("/health");



app.Run();
