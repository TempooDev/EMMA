using Dapper;
using Npgsql;

namespace EMMA.Api.Data.Repositories;

public interface IAssetRepository
{
    Task<IEnumerable<string>> GetAllIdsAsync(CancellationToken ct = default);
    Task<AssetStatusEntity?> GetStatusAsync(string id, CancellationToken ct = default);
}

public class AssetRepository(NpgsqlDataSource dataSource) : IAssetRepository
{
    public async Task<IEnumerable<string>> GetAllIdsAsync(CancellationToken ct = default)
    {
        using var connection = await dataSource.OpenConnectionAsync(ct);
        const string query = "SELECT device_id FROM devices;";
        return await connection.QueryAsync<string>(query);
    }

    public async Task<AssetStatusEntity?> GetStatusAsync(string id, CancellationToken ct = default)
    {
        using var connection = await dataSource.OpenConnectionAsync(ct);
        const string query = @"
            SELECT 
                time as LastHeartbeat,
                power_kw as PowerKw
            FROM asset_metrics
            WHERE asset_id = @Id
            ORDER BY time DESC
            LIMIT 1;";
            
        return await connection.QuerySingleOrDefaultAsync<AssetStatusEntity>(query, new { Id = id });
    }
}

public class AssetStatusEntity
{
    public double? PowerKw { get; set; }
    public DateTimeOffset LastHeartbeat { get; set; }
}
