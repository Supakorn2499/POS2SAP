using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using POS2SAP.API.Common;
using POS2SAP.API.Services.Interfaces;
using POS2SAP.API.Services.Implementations;
using POS2SAP.API.Middleware;
using AspNetCoreRateLimit;

var builder = WebApplication.CreateBuilder(args);

// Run as Windows Service when installed on server (install.ps1)
builder.Host.UseWindowsService();

// ------------------------------------------------------------------ Serilog
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console()
       .WriteTo.File("Logs/pos2sap-.log", rollingInterval: RollingInterval.Day));

// ------------------------------------------------------------------ Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

gbVar.MainConstr = connectionString;

// ------------------------------------------------------------------ JWT Configuration
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("Jwt").Bind(jwtSettings);

const string placeholderSecret = "your-very-long-secret-key-at-least-32-characters-for-hs256";
const string devOnlySecret     = "dev-secret-key-minimum-32-characters-for-dev-only-CHANGE-IN-PRODUCTION";

if (string.IsNullOrWhiteSpace(jwtSettings.Secret) || jwtSettings.Secret.Length < 32)
{
    throw new InvalidOperationException(
        "JWT Secret is required (minimum 32 characters). " +
        "Set Jwt:Secret in appsettings.Development.json for local dev, or JWT__Secret environment variable for production.");
}

if (!builder.Environment.IsDevelopment() &&
    (jwtSettings.Secret == placeholderSecret || jwtSettings.Secret == devOnlySecret))
{
    throw new InvalidOperationException(
        "JWT Secret must not use a placeholder value in non-Development environments. " +
        "Set JWT__Secret to a secure random string (32+ characters).");
}

builder.Services.AddSingleton(jwtSettings);
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// ------------------------------------------------------------------ Authentication
var key = Encoding.ASCII.GetBytes(jwtSettings.Secret);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
    
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Authentication failed: {Message}", context.Exception?.Message);
            return Task.CompletedTask;
        }
    };
});

// ------------------------------------------------------------------ Services — DB (Dapper)
builder.Services.AddScoped<IDbConnection>(_ => new SqlConnection(connectionString));

// ------------------------------------------------------------------ Services — Business
builder.Services.AddScoped<IInterfaceMonitorService, InterfaceMonitorService>();
builder.Services.AddScoped<IPosDataService, PosDataService>();
builder.Services.AddScoped<IInterfaceJobService, InterfaceJobService>();
builder.Services.AddScoped<IGlMappingService, GlMappingService>();
builder.Services.AddScoped<IProductGroupMappingService, ProductGroupMappingService>();
builder.Services.AddScoped<IDeliveryDocTypeService, DeliveryDocTypeService>();

// ------------------------------------------------------------------ Services — Background Job
builder.Services.AddHostedService<InterfaceJobService>();

// ------------------------------------------------------------------ HttpClient — SAP AR Invoice
// HttpClient ceiling — per-request timeout from interface_configs (sap_http_timeout_seconds)
builder.Services.AddHttpClient<ISapArInvoiceService, SapArInvoiceService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(SapHttpHelper.MaxTimeoutSeconds);
});

builder.Services.AddHttpClient<ISapIncomingPaymentService, SapIncomingPaymentService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(SapHttpHelper.MaxTimeoutSeconds);
});

builder.Services.AddHttpClient<ISapDeliveryService, SapDeliveryService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(SapHttpHelper.MaxTimeoutSeconds);
});

// ------------------------------------------------------------------ Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddInMemoryRateLimiting();

// ------------------------------------------------------------------ CORS
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
        ?? new[] { "http://localhost:5173", "http://localhost:3000" };

    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("Authorization");
    });
});

// ------------------------------------------------------------------ Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "POS2SAP API", 
        Version = "v1", 
        Description = "POS to SAP B1 Interface System",
        Contact = new() { Name = "Support", Email = "support@pos2sap.local" }
    });
    
    // Add JWT to Swagger
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter JWT token"
    });
    
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddControllers();

// ------------------------------------------------------------------ Build
var app = builder.Build();

var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var serveSpa = Directory.Exists(wwwrootPath);

app.UseSerilogRequestLogging();

if (serveSpa)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "POS2SAP API v1"));
}

// app.UseIpRateLimiting() must run before auth middleware
app.UseIpRateLimiting();
app.UseCors("AllowFrontend");

// Custom middleware
app.UseMiddleware<JwtAuthMiddleware>();
app.UseMiddleware<AuthorizationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTime.UtcNow }));
app.MapControllers();

if (serveSpa)
{
    app.MapFallbackToFile("index.html");
}

// Ensure POS→SAP mapping tables exist (idempotent; safe on every startup)
using (var scope = app.Services.CreateScope())
{
    var pgMapping = scope.ServiceProvider.GetRequiredService<IProductGroupMappingService>();
    await pgMapping.EnsureSchemaAsync();

    var dlDocTypes = scope.ServiceProvider.GetRequiredService<IDeliveryDocTypeService>();
    await dlDocTypes.EnsureSchemaAsync();

    var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
    await monitor.EnsureScheduleConfigAsync();
}

app.Run();
