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
            LoginRequest request,
            UserManager<ApplicationUser> userManager,
            ITokenService tokenService,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("TokenEndpoints");
            try
            {
                logger.LogInformation("Login attempt for user: {Username}", request.Username);
                var user = await userManager.FindByNameAsync(request.Username);
                if (user == null)
                {
                    logger.LogWarning("User not found: {Username}", request.Username);
                    return Results.Unauthorized();
                }

                var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
                if (!passwordValid)
                {
                    logger.LogWarning("Invalid password for user: {Username}", request.Username);
                    return Results.Unauthorized();
                }

                var token = tokenService.CreateToken(user);
                logger.LogInformation("Token created successfully for user: {Username}", request.Username);
                return Results.Ok(new { access_token = token, token_type = "Bearer", expires_in = 7200 });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during login for user: {Username}", request.Username);
                return Results.Problem("An internal error occurred during login.");
            }
        })
        .DisableAntiforgery();

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
