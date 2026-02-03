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

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddOpenApi();

// Dapper / Npgsql
builder.Services.AddSingleton<NpgsqlDataSource>(sp => 
    NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("emma-db")!));

// Repositories
builder.Services.AddSingleton<IAssetRepository, AssetRepository>();
builder.Services.AddSingleton<IMarketRepository, MarketRepository>();

// Services
builder.Services.AddSingleton<IAssetService, AssetService>();
builder.Services.AddSingleton<IMarketService, MarketService>();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddHttpContextAccessor();

// Authentication & JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "a-very-long-secret-key-that-should-be-in-config";
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

app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapAssetEndpoints();
app.MapMarketEndpoints();

app.Run();
