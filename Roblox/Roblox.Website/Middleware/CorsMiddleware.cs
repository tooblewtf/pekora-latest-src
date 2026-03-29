namespace Roblox.Website.Middleware;

public class RobloxPlayerCorsMiddleware
{
    private RequestDelegate _next;
    public RobloxPlayerCorsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    private string GenerateCspHeader(bool isAuthenticated)
    {
        var connectSrc = "'self' https://*.silrev.biz wss://*.silrev.biz https://hcaptcha.com https://*.hcaptcha.com https://*.cdn.com https://*.archive.org/* https://web.archive.org https://challenges.cloudflare.com/* ws://localhost:*";

        var imgSrc = "'self' data: https://cdn.discordapp.com";
        if (isAuthenticated)
        {
            imgSrc += " https://*.silrev.biz https://*.cdn.com https://*.archive.org http://*.archive.org https://challenges.cloudflare.com/*";
        }

        var scriptSrc =
            "'unsafe-eval' 'self' https://challenges.cloudflare.com/turnstile/v0/api.js https://translate.google.com https://hcaptcha.com https://*.hcaptcha.com https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/js/bootstrap.bundle.min.js https://silrev.biz http://*.archive.org https://*.archive.org http://js.rbxcdn.com/46eace8231bf3c1ce64c55407d9ae60d.js";
        scriptSrc += " https://cdn.jsdelivr.net/npm/cryptocoins-icons@2.9.0/gulpfile.min.js";

        var fontSrc = "'self' https://fonts.gstatic.com https://cdn.jsdelivr.net http://www.silrev.biz https://silrev.biz https://*.silrev.biz https://www.silrev.biz/fonts/GothamSSmBold.woff2 https://www.silrev.biz/fonts/GothamSSmMedium.woff2 https://www.silrev.biz/fonts/GothamSSmBook.woff2";

        var styleSrc = "";

    #if DEBUG
        if (Configuration.BaseUrl.Contains("goober.top")) {
            styleSrc = " https://www.goober.top/fonts/gotham1.css";
            fontSrc += " https://www.goober.top/fonts/GothamSSmBold.woff2 https://www.goober.top/fonts/GothamSSmMedium.woff2 https://www.goober.top/fonts/GothamSSmBook.woff2 https://www.goober.top/fonts/GothamSSmLight.woff2 https://www.goober.top/fonts/GothamSSmBlack.woff2";
            imgSrc += " https://*.silrev.biz";
        }
    #endif

        // add cryptocoins-icons stylesheet
        styleSrc += " https://cdn.jsdelivr.net/npm/cryptocoins-icons@2.9.0/webfont/cryptocoins.min.css";

        return "default-src 'self'; img-src " + imgSrc + "; child-src 'self'; script-src " + scriptSrc + "; frame-src 'self' https://hcaptcha.com https://challenges.cloudflare.com http://challenges.cloudflare.com https://*.archive.org; style-src 'unsafe-inline' 'self' http://*.archive.org https://fonts.googleapis.com https://hcaptcha.com https://*.hcaptcha.com https://silrev.biz https://www.silrev.biz https://cdn.jsdelivr.net/npm/bootstrap-icons/font/bootstrap-icons.css https://cdn.jsdelivr.net/gh/AllienWorks/cryptocoins@2.7.0/webfont/cryptocoins.css https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css https://silrev.biz/fonts/gotham1.css http://*.silrev.biz" + styleSrc + "; font-src " + fontSrc + "; connect-src " + connectSrc + "; worker-src 'self';";
    }


    public async Task InvokeAsync(HttpContext ctx)
    {
        var isAuthenticated = ctx.Items.ContainsKey(SessionMiddleware.CookieName);
        ctx.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
        ctx.Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
        ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
        ctx.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        ctx.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        ctx.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["Content-Security-Policy"] = GenerateCspHeader(isAuthenticated);
        await _next(ctx);
    }
}

public static class RobloxPlayerCorsMiddlewareExtensions
{
    public static IApplicationBuilder UseRobloxPlayerCorsMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RobloxPlayerCorsMiddleware>();
    }
}