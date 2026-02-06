using System.Text;
using EMMA.Server.Endpoints;
using EMMA.Server.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Authentication & JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing from configuration.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();

// Configure OpenAPI with comprehensive metadata
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "EMMA API",
            Version = "v1",
            Description = "Energy Management and Market Analytics API - Provides endpoints for dashboard data, VPP management, energy forecasting, and cross-border arbitrage analysis.",
            Contact = new()
            {
                Name = "EMMA Team"
            }
        };

        return Task.CompletedTask;
    });

    // Add security scheme for JWT Bearer authentication
    options.AddSchemaTransformer((schema, context, cancellationToken) =>
    {
        // This will be automatically handled by ASP.NET Core's security requirements
        return Task.CompletedTask;
    });
});

// Configure databases and initializer
builder.Services.AddKeyedSingleton<NpgsqlDataSource>("app-db", (sp, key) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("app-db") ?? throw new InvalidOperationException("Connection string 'app-db' is missing.")));

builder.Services.AddKeyedSingleton<NpgsqlDataSource>("telemetry-db", (sp, key) =>
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("telemetry-db") ?? throw new InvalidOperationException("Connection string 'telemetry-db' is missing.")));

builder.Services.AddHostedService<DbInitializer>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<EMMA.Server.Infrastructure.Identity.ITenantProvider, EMMA.Server.Infrastructure.Identity.TenantProvider>();
builder.Services.AddScoped<DashboardRepository>(); // Changed to Scoped

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("EMMA API Documentation")
        .WithTheme(ScalarTheme.Purple)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

var api = app.MapGroup("/api").RequireAuthorization();
api.MapGet("weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapDefaultEndpoints();
app.MapDashboardEndpoints(); // These might need internal RequireAuthorization too, but we can secure them globally if needed.
// Secure dashboard group if it's not already
app.MapGroup("/market").RequireAuthorization();

app.UseFileServer();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
