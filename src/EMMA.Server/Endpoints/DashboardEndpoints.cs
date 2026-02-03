using EMMA.Server.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace EMMA.Server.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard");

        group.MapGet("/energy-mix", async (
            [FromServices] DashboardRepository repository, 
            [FromQuery] DateTimeOffset? start,
            [FromQuery] DateTimeOffset? end,
            [FromQuery] string? bucket,
            CancellationToken ct = default) =>
        {
            var endTime = end ?? DateTimeOffset.UtcNow;
            var startTime = start ?? endTime.AddHours(-24); // Default to last 24h
            var timeBucket = bucket ?? "1 minute";

            var data = await repository.GetEnergyMixAsync(startTime, endTime, timeBucket, ct);
            return Results.Ok(data);
        })
        .WithName("GetEnergyMix")
        .WithOpenApi();
        group.MapGet("/devices-status", async (
            [FromServices] DashboardRepository repository, 
            CancellationToken ct = default) =>
        {
            var data = await repository.GetDeviceStatusAsync(ct);
            return Results.Ok(data);
        })
        .WithName("GetDeviceStatus")
        .WithOpenApi();
    }
}
