using System.Security.Claims;
using Emma.Identity.Models;
using Emma.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Emma.Identity.Endpoints;

public static class TokenEndpoints
{
    public static void MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/connect/token", async (
            [FromForm] string username, 
            [FromForm] string password,
            UserManager<ApplicationUser> userManager,
            ITokenService tokenService) =>
        {
            var user = await userManager.FindByNameAsync(username);
            if (user == null || !await userManager.CheckPasswordAsync(user, password))
            {
                return Results.Unauthorized();
            }

            var token = tokenService.CreateToken(user);
            return Results.Ok(new { access_token = token, token_type = "Bearer", expires_in = 7200 });
        })
        .DisableAntiforgery(); // Simplified for microservice token endpoint

        app.MapGet("/userinfo", (ClaimsPrincipal user) =>
        {
            return Results.Ok(new
            {
                sub = user.FindFirstValue(ClaimTypes.NameIdentifier),
                email = user.FindFirstValue(ClaimTypes.Email),
                tenant_id = user.FindFirstValue("tenant_id"),
                assigned_assets = user.FindFirstValue("assigned_assets")
            });
        })
        .RequireAuthorization();
    }
}
 public record LoginRequest(string Username, string Password);
