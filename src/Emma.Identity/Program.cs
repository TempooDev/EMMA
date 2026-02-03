using System.Text;
using Emma.Identity.Data;
using Emma.Identity.Endpoints;
using Emma.Identity.Models;
using Emma.Identity.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Database
builder.Services.AddDbContext<IdentityDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("emma-db") 
        ?? throw new InvalidOperationException("Connection string 'emma-db' is missing.");
    options.UseNpgsql(connectionString);
});

builder.Services.AddSingleton<Npgsql.NpgsqlDataSource>(sp => 
{
    var connectionString = builder.Configuration.GetConnectionString("emma-db") 
        ?? throw new InvalidOperationException("Connection string 'emma-db' is missing.");
    return Npgsql.NpgsqlDataSource.Create(connectionString);
});

// Identity
builder.Services.AddIdentityCore<ApplicationUser>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 4;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<IdentityDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddOpenApi();

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
builder.Services.AddScoped<ITokenService, TokenService>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapTokenEndpoints();
app.MapApiKeyEndpoints();

// Seed data or migrations could go here
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    db.Database.EnsureCreated();
    
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    if (await userManager.FindByNameAsync("admin") == null)
    {
        var admin = new ApplicationUser 
        { 
            UserName = "admin", 
            Email = "admin@emma.ai",
            TenantId = "T001",
            AssignedAssets = "asset-001,asset-002"
        };
        await userManager.CreateAsync(admin, "Admin123!");
    }
}

app.Run();
