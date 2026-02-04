using Dapper;
using EMMA.Api.Infrastructure.Identity;
using Npgsql;

namespace EMMA.Api.Features.Assets.Data;

public interface IAssetRepository
{
    Task<IEnumerable<string>> GetAllIdsAsync(CancellationToken ct = default);
    Task<AssetStatusEntity?> GetStatusAsync(string id, CancellationToken ct = default);
}

public class AssetRepository(
    [FromKeyedServices("app-db")] NpgsqlDataSource appDataSource,
    [FromKeyedServices("telemetry-db")] NpgsqlDataSource telemetryDataSource,
    ITenantProvider tenantProvider) : IAssetRepository
{
    public async Task<IEnumerable<string>> GetAllIdsAsync(CancellationToken ct = default)
    {
        var tenantId = tenantProvider.TenantId;
        using var connection = await appDataSource.OpenConnectionAsync(ct);

        string query = "SELECT device_id FROM devices WHERE tenant_id = @TenantId";
        if (tenantProvider.IsSandbox)
        {
            query += " AND device_id LIKE 'sim_%'";
        }
        query += ";";

        return await connection.QueryAsync<string>(query, new { TenantId = tenantId });
    }

    public async Task<AssetStatusEntity?> GetStatusAsync(string id, CancellationToken ct = default)
    {
        var tenantId = tenantProvider.TenantId;

        // 1. Verify ownership in app-db
        using var appConn = await appDataSource.OpenConnectionAsync(ct);
        string verifyQuery = "SELECT COUNT(1) FROM devices WHERE device_id = @Id AND tenant_id = @TenantId";
        if (tenantProvider.IsSandbox) verifyQuery += " AND device_id LIKE 'sim_%'";

        var exists = await appConn.ExecuteScalarAsync<bool>(verifyQuery, new { Id = id, TenantId = tenantId });
        if (!exists) return null;

        // 2. Fetch latest metrics from telemetry-db
        using var teleConn = await telemetryDataSource.OpenConnectionAsync(ct);
        string query = @"
            SELECT 
                time as LastHeartbeat,
                power_kw as PowerKw
            FROM asset_metrics
            WHERE asset_id = @Id
            ORDER BY time DESC 
            LIMIT 1;";

        return await teleConn.QuerySingleOrDefaultAsync<AssetStatusEntity>(query, new { Id = id });
    }
}

public class AssetStatusEntity
{
    public double? PowerKw { get; set; }
    public DateTimeOffset LastHeartbeat { get; set; }
}
