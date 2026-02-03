using Mapster;
using Npgsql;
using Scalar.AspNetCore;
using EMMA.Api.Features.Assets;
using EMMA.Api.Features.Assets.Data;
using EMMA.Api.Features.Market;
using EMMA.Api.Features.Market.Data;
using EMMA.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddOpenApi(options => 
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "EMMA Developer API";
        document.Info.Description = "Real-time energy market results and asset control for third-party developers.";
        document.Info.Version = "v1.0";
        
        document.Components ??= new Microsoft.OpenApi.Models.OpenApiComponents(); // Wait, let's try without .Models
        document.Components.SecuritySchemes.Add("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "JWT Authorization header using the Bearer scheme."
        });
        document.Components.SecuritySchemes.Add("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Name = "X-API-KEY",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "API Key authentication using the X-API-KEY header."
        });

        return Task.CompletedTask;
    });
});

// Dapper / Npgsql
builder.Services.AddSingleton<NpgsqlDataSource>(sp => 
{
    var connectionString = builder.Configuration.GetConnectionString("emma-db") 
        ?? throw new InvalidOperationException("Connection string 'emma-db' is missing.");
    return NpgsqlDataSource.Create(connectionString);
});

// Repositories
builder.Services.AddSingleton<IAssetRepository, AssetRepository>();
builder.Services.AddSingleton<IMarketRepository, MarketRepository>();

// Services
builder.Services.AddSingleton<IAssetService, AssetService>();
builder.Services.AddSingleton<IMarketService, MarketService>();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddHttpContextAccessor();

// Authentication & JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing from configuration.");
builder.Services.AddAuthentication(options => 
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
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
    })
    .AddScheme<EMMA.Api.Infrastructure.Auth.ApiKeyAuthenticationOptions, EMMA.Api.Infrastructure.Auth.ApiKeyAuthenticationHandler>(
        EMMA.Api.Infrastructure.Auth.ApiKeyAuthenticationOptions.DefaultScheme, null);

builder.Services.AddAuthorization(options =>
{
    var defaultPolicyBuilder = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
        JwtBearerDefaults.AuthenticationScheme,
        EMMA.Api.Infrastructure.Auth.ApiKeyAuthenticationOptions.DefaultScheme);
    defaultPolicyBuilder.RequireAuthenticatedUser();
    options.DefaultPolicy = defaultPolicyBuilder.Build();
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    options.AddFixedWindowLimiter("api-key-limit", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueLimit = 0;
    });
});

// Mapster
TypeAdapterConfig.GlobalSettings.Default.PreserveReference(true);

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<EMMA.Api.Infrastructure.Logging.AuditMiddleware>();

// Endpoints
app.MapAssetEndpoints();
app.MapMarketEndpoints();

app.Run();
