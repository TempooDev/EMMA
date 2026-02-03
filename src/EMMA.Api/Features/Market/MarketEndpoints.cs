using EMMA.Api.Services;

namespace EMMA.Api.Features.Market;

public static class MarketEndpoints
{
    public static void MapMarketEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/market/summary", async (IMarketService service, CancellationToken ct) =>
        {
            var summary = await service.GetMarketSummaryAsync(ct);
            return Results.Ok(summary);
        })
        .WithName("GetMarketSummary");
    }
}
