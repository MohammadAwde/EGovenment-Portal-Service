using Microsoft.AspNetCore.Http;

namespace SmartEGov.Infrastructure.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        // X-XSS-Protection is obsolete in modern browsers; remove to avoid unnecessary header
        context.Response.Headers.Remove("X-XSS-Protection");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        // Disable payment handler registration (payment) to avoid browsers attempting to fetch payment manifests from third-party providers
        context.Response.Headers.Append("Permissions-Policy", "geolocation=*, microphone=(), camera=(), payment=()");

        context.Response.Headers.Append(
            "Content-Security-Policy",
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://js.stripe.com https://checkout.stripe.com https://unpkg.com; " +
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://unpkg.com; " +
            "img-src 'self' data: https: https://*.tile.openstreetmap.org https://raw.githubusercontent.com; " +
            "font-src 'self' https://cdn.jsdelivr.net data:; " +
            "connect-src 'self' https://api.stripe.com https://hooks.stripe.com https://pay.google.com https://google.com https://*.tile.openstreetmap.org https://api.mymemory.translated.net https://lingva.ml https://translate.plausibility.cloud https://lingva.garudalinux.org https://unpkg.com https://api-free.deepl.com; " +
            "manifest-src 'self' https://pay.google.com https://google.com; " +
            "frame-ancestors 'none';"
);

        await _next(context);
    }
}
