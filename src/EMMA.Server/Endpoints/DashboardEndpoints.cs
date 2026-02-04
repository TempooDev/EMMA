using EMMA.Server.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace EMMA.Server.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").RequireAuthorization();

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
        .WithSummary("Get energy mix data")
        .WithDescription("Retrieves time-series energy mix data showing the breakdown of energy sources (solar, wind, etc.) over a specified time period. Defaults to the last 24 hours if no time range is provided.")
        .WithTags("Dashboard")
        .Produces<object>(StatusCodes.Status200OK)
        .RequireAuthorization();

        group.MapGet("/devices-status", async (
            [FromServices] DashboardRepository repository,
            CancellationToken ct = default) =>
        {
            var data = await repository.GetDeviceStatusAsync(ct);
            return Results.Ok(data);
        })
        .WithName("GetDeviceStatus")
        .WithSummary("Get device status")
        .WithDescription("Retrieves the current operational status of all registered IoT devices including solar inverters, batteries, and other energy assets.")
        .WithTags("Dashboard")
        .Produces<object>(StatusCodes.Status200OK)
        .RequireAuthorization();

        group.MapGet("/vpp/capacity", async (
            [FromServices] DashboardRepository repository,
            CancellationToken ct = default) =>
        {
            var data = await repository.GetVppCapacityByZoneAsync(ct);
            return Results.Ok(data);
        })
        .WithName("GetVppCapacity")
        .WithSummary("Get VPP capacity by zone")
        .WithDescription("Retrieves the aggregated Virtual Power Plant (VPP) capacity breakdown by market zone, showing available flexible capacity for demand response.")
        .WithTags("VPP")
        .Produces<object>(StatusCodes.Status200OK)
        .RequireAuthorization();

        group.MapPost("/vpp/bid", async (
            [FromServices] DashboardRepository repository,
            [FromBody] FlexibilityBidRequest request,
            CancellationToken ct = default) =>
        {
            await repository.InsertFlexibilityBidAsync(request.MarketZone, request.ReductionMw, request.PricePerMwh, cts: ct);
            return Results.Accepted();
        })
        .WithName("SubmitFlexibilityBid")
        .WithSummary("Submit flexibility bid")
        .WithDescription("Submits a flexibility bid to the market, offering to reduce consumption by a specified amount (in MW) at a given price per MWh for a specific market zone.")
        .WithTags("VPP")
        .Accepts<FlexibilityBidRequest>("application/json")
        .Produces(StatusCodes.Status202Accepted)
        .RequireAuthorization();

        group.MapGet("/arbitrage", async (
            [FromServices] DashboardRepository repository,
            CancellationToken ct = default) =>
        {
            var data = await repository.GetCrossBorderArbitrageAsync(ct);
            return data != null ? Results.Ok(data) : Results.NotFound();
        })
        .WithName("GetCrossBorderArbitrage")
        .WithSummary("Get cross-border arbitrage opportunities")
        .WithDescription("Retrieves current cross-border arbitrage opportunities between interconnected market zones (e.g., Spain-France), showing price differentials and potential profit margins.")
        .WithTags("Market Analysis")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization();

        group.MapGet("/impact-metrics", async (
            [FromServices] DashboardRepository repository,
            CancellationToken ct = default) =>
        {
            var metrics = await repository.GetImpactMetricsAsync(ct);
            return metrics is not null ? Results.Ok(metrics) : Results.NotFound();
        })
        .WithName("GetImpactMetrics")
        .WithSummary("Get environmental impact metrics")
        .WithDescription("Retrieves environmental impact metrics including CO2 emissions avoided, renewable energy percentage, and other sustainability KPIs.")
        .WithTags("Dashboard")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization();

        group.MapGet("/price-forecast", async (
            [FromServices] DashboardRepository repository,
            CancellationToken ct = default) =>
        {
            var data = await repository.GetPriceForecastAsync(ct);
            return Results.Ok(data);
        })
        .WithName("GetPriceForecast")
        .WithSummary("Get energy price forecast")
        .WithDescription("Retrieves energy price forecasts for upcoming time periods, helping to optimize energy consumption and trading strategies.")
        .WithTags("Market Analysis")
        .Produces<object>(StatusCodes.Status200OK)
        .RequireAuthorization();
    }
}

public record FlexibilityBidRequest(string MarketZone, double ReductionMw, double? PricePerMwh);
