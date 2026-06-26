using System.IdentityModel.Tokens.Jwt;
using POS2SAP.API.Common;

namespace POS2SAP.API.Middleware;

public class JwtAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtAuthMiddleware> _logger;

    public JwtAuthMiddleware(RequestDelegate next, ILogger<JwtAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IJwtTokenService jwtTokenService)
    {
        var token = ExtractTokenFromHeader(context);

        if (!string.IsNullOrEmpty(token))
        {
            var principal = jwtTokenService.ValidateToken(token);
            if (principal != null)
            {
                context.User = principal;
            }
            else
            {
                _logger.LogWarning("Invalid token attempt");
            }
        }

        await _next(context);
    }

    private static string? ExtractTokenFromHeader(HttpContext context)
    {
        const string authorizationHeaderName = "Authorization";
        const string bearerPrefix = "Bearer ";

        if (!context.Request.Headers.ContainsKey(authorizationHeaderName))
            return null;

        var authorizationHeader = context.Request.Headers[authorizationHeaderName].ToString();
        if (string.IsNullOrEmpty(authorizationHeader))
            return null;

        if (authorizationHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return authorizationHeader[bearerPrefix.Length..];
        }

        return null;
    }
}

public class AuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthorizationMiddleware> _logger;

    // Only auth endpoints and health check are public. Swagger is dev-only (see Program.cs).
    private static readonly string[] PublicRoutes =
    [
        "/api/auth/login",
        "/api/auth/refresh",
        "/health"
    ];

    public AuthorizationMiddleware(RequestDelegate next, ILogger<AuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        if (IsPublicRoute(path) || IsSwaggerRoute(path) || IsSpaStaticRoute(path))
        {
            await _next(context);
            return;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Unauthorized access attempt to {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Unauthorized" });
            return;
        }

        await _next(context);
    }

    private static bool IsPublicRoute(string path) =>
        PublicRoutes.Any(route => path.StartsWith(route, StringComparison.OrdinalIgnoreCase));

    private static bool IsSwaggerRoute(string path) =>
        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);

    /// <summary>Production UI (wwwroot) — login page and assets are public; /api stays protected.</summary>
    private static bool IsSpaStaticRoute(string path) =>
        !path.StartsWith("/api", StringComparison.OrdinalIgnoreCase);
}
