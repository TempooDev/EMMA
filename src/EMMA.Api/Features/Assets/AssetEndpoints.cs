
namespace EMMA.Api.Features.Assets;

public static class AssetEndpoints
{
    public static void MapAssetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/assets", async (IAssetService service, CancellationToken ct) =>
        {
            var ids = await service.GetAssetsAsync(ct);
            return Results.Ok(ids);
        })
        .WithName("GetAssets")
        .RequireAuthorization();

        app.MapGet("/assets/{id}/status", async (string id, IAssetService service, CancellationToken ct) =>
        {
            var status = await service.GetAssetStatusAsync(id, ct);
            return status != null ? Results.Ok(status) : Results.NotFound();
        })
        .WithName("GetAssetStatus")
        .RequireAuthorization();
    }
}
