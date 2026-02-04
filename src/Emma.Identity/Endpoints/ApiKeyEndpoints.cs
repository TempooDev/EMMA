using System.Security.Claims;
using Dapper;
using Emma.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace Emma.Identity.Endpoints;

public static class ApiKeyEndpoints
{
    public static void MapApiKeyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/keys").RequireAuthorization();

        group.MapPost("/", async (ClaimsPrincipal user, NpgsqlDataSource dataSource) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tenantId = user.FindFirst("tenant_id")?.Value;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
            {
                return Results.Unauthorized();
            }

            var apiKey = Guid.NewGuid().ToString("N");

            using var connection = await dataSource.OpenConnectionAsync();
            await connection.ExecuteAsync(
                "INSERT INTO api_keys (key, owner_id, tenant_id) VALUES (@Key, @OwnerId, @TenantId)",
                new { Key = apiKey, OwnerId = userId, TenantId = tenantId });

            return Results.Ok(new { ApiKey = apiKey });
        })
        .WithName("GenerateApiKey")
        .WithSummary("Generate new API key")
        .WithDescription("Creates a new API key for the authenticated user. The API key can be used for programmatic access to EMMA APIs without requiring JWT authentication.")
        .WithTags("API Keys")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, NpgsqlDataSource dataSource) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            using var connection = await dataSource.OpenConnectionAsync();
            var keys = await connection.QueryAsync(
                "SELECT id, created_at as CreatedAt, is_active as IsActive FROM api_keys WHERE owner_id = @UserId",
                new { UserId = userId });

            return Results.Ok(keys);
        })
        .WithName("ListApiKeys")
        .WithSummary("List user's API keys")
        .WithDescription("Retrieves all API keys belonging to the authenticated user, including their creation date and active status.")
        .WithTags("API Keys")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
    }
}
