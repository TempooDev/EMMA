using Dapper;
using EMMA.Server.Infrastructure.Identity;
using EMMA.Shared;
using Npgsql;

namespace EMMA.Server.Infrastructure.Data;

public class DashboardRepository(NpgsqlDataSource dataSource, ITenantProvider tenantProvider)
{
    public async Task<IEnumerable<EnergyMixDto>> GetEnergyMixAsync(DateTimeOffset start, DateTimeOffset end, string bucket, CancellationToken ct)
    {
        var tenantId = tenantProvider.TenantId;
        using var connection = await dataSource.OpenConnectionAsync(ct);

        // ... existing bucket validation ...
        var allowedBuckets = new HashSet<string> { "5 seconds", "30 seconds", "1 minute", "5 minutes", "15 minutes", "30 minutes", "1 hour", "1 day" };
        if (!allowedBuckets.Contains(bucket))
        {
            bucket = "1 minute"; // Fallback default
        }

        return await connection.QueryAsync<EnergyMixDto>(Queries.GetEnergyMix, new { Start = start, End = end, Bucket = bucket, TenantId = tenantId });
    }
    public async Task<IEnumerable<DeviceStatusDto>> GetDeviceStatusAsync(CancellationToken ct)
    {
        var tenantId = tenantProvider.TenantId;
        using var connection = await dataSource.OpenConnectionAsync(ct);

        // Fetch devices and their LATEST metric (using DISTINCT ON for efficiency in Postgres)
        // Note: DISTINCT ON (asset_id) ORDER BY asset_id, time DESC gives the last row per asset.
        return await connection.QueryAsync<DeviceStatusDto>(Queries.GetDeviceStatus, new { TenantId = tenantId });
    }

    public async Task<IEnumerable<dynamic>> GetVppCapacityByZoneAsync(CancellationToken cts = default)
    {
        using var connection = await dataSource.OpenConnectionAsync(cts);
        return await connection.QueryAsync<dynamic>(Queries.GetVppCapacityByZone);
    }

    public async Task InsertFlexibilityBidAsync(string marketZone, double reductionMw, double? pricePerMwh, string status = "SUBMITTED", CancellationToken cts = default)
    {
        using var connection = await dataSource.OpenConnectionAsync(cts);
        await connection.ExecuteAsync(Queries.InsertFlexibilityBid, new
        {
            MarketZone = marketZone,
            ReductionMw = reductionMw,
            PricePerMwh = pricePerMwh,
            Status = status
        });
    }

    public async Task<ArbitrageDto?> GetCrossBorderArbitrageAsync(CancellationToken cts = default)
    {
        using var connection = await dataSource.OpenConnectionAsync(cts);
        return await connection.QueryFirstOrDefaultAsync<ArbitrageDto>(Queries.GetCrossBorderArbitrage);
    }

    public async Task<ImpactMetricsDto?> GetImpactMetricsAsync(CancellationToken cts = default)
    {
        var tenantId = tenantProvider.TenantId;
        using var connection = await dataSource.OpenConnectionAsync(cts);
        return await connection.QueryFirstOrDefaultAsync<ImpactMetricsDto>(Queries.GetImpactMetrics, new { TenantId = tenantId });
    }
}

public class ArbitrageDto
{
    public double? PriceEs { get; set; }
    public double? PriceFr { get; set; }
    public double? PhysicalFlowMw { get; set; }
    public double? NtcMw { get; set; }
    public double? SaturationPercentage { get; set; }
    public string? FlowDirection { get; set; }
}

public class ImpactMetricsDto
{
    public double TotalSavingsEur { get; set; }
    public double NegativePriceEnergyKwh { get; set; }
    public double CurrentPriceEurMwh { get; set; }
}

public class DeviceStatusDto
{
    public string DeviceId { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? CurrentPowerKw { get; set; }
    public double? Temperature { get; set; }
    public DateTimeOffset? LastUpdated { get; set; }
    public bool IsChargingNegativePrice { get; set; }
}

public class EnergyMixDto
{
    public DateTimeOffset Time { get; set; }
    public double? PowerKw { get; set; }
    public double? PricePerMwh { get; set; }
}
