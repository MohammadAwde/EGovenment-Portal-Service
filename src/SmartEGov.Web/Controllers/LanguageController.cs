using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace SmartEGov.Web.Controllers;

public class LanguageController : Controller
{
    [HttpPost]
    public IActionResult Switch(string culture, string returnUrl = "/")
    {
        // Validate culture
        var supportedCultures = new[] { "en", "ar" };
        if (!supportedCultures.Contains(culture))
            culture = "en";

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(
                new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });

        return LocalRedirect(returnUrl);
    }

    [HttpGet]
    public IActionResult Switch(string culture, string returnUrl = "/", string method = "get")
    {
        var supportedCultures = new[] { "en", "ar" };
        if (!supportedCultures.Contains(culture))
            culture = "en";

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(
                new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });

        return LocalRedirect(returnUrl);
    }
}
