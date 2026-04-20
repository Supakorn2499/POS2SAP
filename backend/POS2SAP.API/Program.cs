using System.Data;
using Microsoft.Data.SqlClient;
using Serilog;
using POS2SAP.API.Common;
using POS2SAP.API.Services.Interfaces;
using POS2SAP.API.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

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

// ------------------------------------------------------------------ Services — DB (Dapper)
builder.Services.AddScoped<IDbConnection>(_ => new SqlConnection(connectionString));

// ------------------------------------------------------------------ Services — Business
builder.Services.AddScoped<IInterfaceMonitorService, InterfaceMonitorService>();
builder.Services.AddScoped<IPosDataService, PosDataService>();
builder.Services.AddScoped<IInterfaceJobService, InterfaceJobService>();

// ------------------------------------------------------------------ Services — Background Job
builder.Services.AddHostedService<InterfaceJobService>();

// ------------------------------------------------------------------ HttpClient — SAP
builder.Services.AddHttpClient<ISapArInvoiceService, SapArInvoiceService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ------------------------------------------------------------------ CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? new[] { "http://localhost:5173", "http://localhost:3000" })
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// ------------------------------------------------------------------ Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "POS2SAP API", Version = "v1", Description = "POS to SAP B1 Interface System" });
});

builder.Services.AddControllers();

// ------------------------------------------------------------------ Build
var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "POS2SAP API v1"));
}

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
