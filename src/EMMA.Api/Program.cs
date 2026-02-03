using Mapster;
using Npgsql;
using Scalar.AspNetCore;
using EMMA.Api.Features.Assets;
using EMMA.Api.Features.Market;
using EMMA.Api.Data.Repositories;
using EMMA.Api.Services;

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

// Endpoints
app.MapAssetEndpoints();
app.MapMarketEndpoints();

app.Run();
