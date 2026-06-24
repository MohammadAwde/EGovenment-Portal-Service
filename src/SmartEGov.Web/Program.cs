using Microsoft.AspNetCore.Identity;
using SmartEGov.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using SmartEGov.Infrastructure;
using SmartEGov.Infrastructure.Data;
using SmartEGov.Infrastructure.Authorization;
using SmartEGov.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;


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

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Seed roles and default Admin user
try
{
    using (var scope = app.Services.CreateScope())
    {
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
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");



app.Run();
