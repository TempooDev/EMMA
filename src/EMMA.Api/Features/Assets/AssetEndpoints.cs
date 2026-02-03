using Dapper;
using Npgsql;
using Microsoft.AspNetCore.Mvc;

namespace EMMA.Api.Features.Assets;

public static class AssetEndpoints
{
    public static void MapAssetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/assets/{id}/status", async (string id, NpgsqlDataSource dataSource) =>
        {
            using var connection = await dataSource.OpenConnectionAsync();
            
            // Get latest metric (raw for real-time status)
            // Using logic similar to DashboardRepository but simplified for single asset
            // We want the LATEST status.
            
            const string query = @"
                SELECT 
                    time as LastHeartbeat,
                    power_kw as PowerKw,
                    NULL as ActiveCommands -- Placeholder as we don't store active commands in metrics yet
                FROM asset_metrics
                WHERE asset_id = @Id
                ORDER BY time DESC
                LIMIT 1;";
                
            var status = await connection.QuerySingleOrDefaultAsync<AssetStatusDto>(query, new { Id = id });
            
            if (status == null) return Results.NotFound();
            
            return Results.Ok(status);
        })
        .WithName("GetAssetStatus");
    }
}

public class AssetStatusDto
{
    public double? PowerKw { get; set; }
    public DateTimeOffset LastHeartbeat { get; set; }
    public List<string> ActiveCommands { get; set; } = new();
}
