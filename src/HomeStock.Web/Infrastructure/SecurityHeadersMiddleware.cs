namespace HomeStock.Web.Infrastructure;

/// <summary>Options for the security-headers middleware (bound from the "Security" section).</summary>
public class SecurityHeaderOptions
{
    public const string SectionName = "Security";

    public bool EnableContentSecurityPolicy { get; set; } = true;

    /// <summary>
    /// Content-Security-Policy value. The default is compatible with Blazor Web App (Interactive
    /// Server): same-origin scripts/styles/connections, inline styles/scripts allowed (needed for
    /// the framework's import map and scoped styles), data: images (QR codes), and blob: workers
    /// (the camera barcode scanner). Override to tighten with nonces once you've tested your setup.
    /// </summary>
    public string ContentSecurityPolicy { get; set; } =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "frame-ancestors 'none'; " +
        "object-src 'none'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "script-src 'self' 'wasm-unsafe-eval' 'unsafe-inline'; " +
        "connect-src 'self' ws: wss:; " +
        "worker-src 'self' blob:; " +
        "form-action 'self'";

    /// <summary>camera=self lets the barcode scanner use the camera; other powerful features off.</summary>
    public string PermissionsPolicy { get; set; } = "camera=(self), microphone=(), geolocation=()";
}

/// <summary>
/// Adds defensive HTTP response headers on every request. These mitigate MIME sniffing,
/// clickjacking, referrer leakage, and constrain resource loading (CSP).
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next, SecurityHeaderOptions options)
{
    public async Task Invoke(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        if (!string.IsNullOrWhiteSpace(options.PermissionsPolicy))
            headers["Permissions-Policy"] = options.PermissionsPolicy;
        if (options.EnableContentSecurityPolicy && !string.IsNullOrWhiteSpace(options.ContentSecurityPolicy))
            headers["Content-Security-Policy"] = options.ContentSecurityPolicy;

        await next(context);
    }
}

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this WebApplication app)
    {
        var options = app.Configuration.GetSection(SecurityHeaderOptions.SectionName).Get<SecurityHeaderOptions>()
                      ?? new SecurityHeaderOptions();
        return app.UseMiddleware<SecurityHeadersMiddleware>(options);
    }
}
