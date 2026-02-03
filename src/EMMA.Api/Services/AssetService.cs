using EMMA.Api.Data.Repositories;
using Mapster;

namespace EMMA.Api.Services;

public interface IAssetService
{
    Task<IEnumerable<string>> GetAssetsAsync(CancellationToken ct = default);
    Task<AssetStatusResponse?> GetAssetStatusAsync(string id, CancellationToken ct = default);
}

public class AssetService(IAssetRepository repository) : IAssetService
{
    public async Task<IEnumerable<string>> GetAssetsAsync(CancellationToken ct = default)
    {
        return await repository.GetAllIdsAsync(ct);
    }

    public async Task<AssetStatusResponse?> GetAssetStatusAsync(string id, CancellationToken ct = default)
    {
        var entity = await repository.GetStatusAsync(id, ct);
        if (entity == null) return null;

        var response = entity.Adapt<AssetStatusResponse>();
        response.ActiveCommands = new List<string>(); // Mocked for now
        return response;
    }
}

public class AssetStatusResponse
{
    public double? PowerKw { get; set; }
    public DateTimeOffset LastHeartbeat { get; set; }
    public List<string> ActiveCommands { get; set; } = new();
}
