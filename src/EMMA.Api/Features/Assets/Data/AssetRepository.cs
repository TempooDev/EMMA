using Dapper;
using EMMA.Api.Infrastructure.Identity;
using Npgsql;

namespace EMMA.Api.Features.Assets.Data;

public interface IAssetRepository
{
    Task<IEnumerable<string>> GetAllIdsAsync(CancellationToken ct = default);
    Task<AssetStatusEntity?> GetStatusAsync(string id, CancellationToken ct = default);
}

public class AssetRepository(NpgsqlDataSource dataSource, ITenantProvider tenantProvider) : IAssetRepository
{
    public async Task<IEnumerable<string>> GetAllIdsAsync(CancellationToken ct = default)
    {
        var tenantId = tenantProvider.GetTenantId();
        using var connection = await dataSource.OpenConnectionAsync(ct);
        const string query = "SELECT device_id FROM devices WHERE tenant_id = @TenantId;";
        return await connection.QueryAsync<string>(query, new { TenantId = tenantId });
    }

    public async Task<AssetStatusEntity?> GetStatusAsync(string id, CancellationToken ct = default)
    {
        var tenantId = tenantProvider.GetTenantId();
        using var connection = await dataSource.OpenConnectionAsync(ct);
        
        // Re-check tenant_id for the specific asset to prevent unauthorized access by ID
        const string query = @"
            SELECT 
                m.time as LastHeartbeat,
                m.power_kw as PowerKw
            FROM asset_metrics m
            JOIN devices d ON m.asset_id = d.device_id
            WHERE d.device_id = @Id AND d.tenant_id = @TenantId
            ORDER BY m.time DESC
            LIMIT 1;";
            
        return await connection.QuerySingleOrDefaultAsync<AssetStatusEntity>(query, new { Id = id, TenantId = tenantId });
    }
}

public class AssetStatusEntity
{
    public double? PowerKw { get; set; }
    public DateTimeOffset LastHeartbeat { get; set; }
}
