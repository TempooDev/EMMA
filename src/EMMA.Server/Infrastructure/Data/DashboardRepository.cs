using Dapper;
using EMMA.Shared;
using Npgsql;

namespace EMMA.Server.Infrastructure.Data;

public class DashboardRepository(NpgsqlDataSource dataSource)
{
    public async Task<IEnumerable<EnergyMixDto>> GetEnergyMixAsync(DateTimeOffset start, DateTimeOffset end, string bucket, CancellationToken ct)
    {
        using var connection = await dataSource.OpenConnectionAsync(ct);

        // Validate bucket to prevent SQL injection (allowlist)
        var allowedBuckets = new HashSet<string> { "5 seconds", "30 seconds", "1 minute", "5 minutes", "15 minutes", "30 minutes", "1 hour", "1 day" };
        if (!allowedBuckets.Contains(bucket))
        {
            bucket = "1 minute"; // Fallback default
        }

        // Query: Join Asset Metrics (minute average) with Market Prices (hourly)
        // We use the same bucket for both if possible, or we keep market prices at 1 hour if the bucket is smaller?
        // Actually, market prices are hourly. If we zoom into 5 seconds, we still want the hourly price.
        // So we should time_bucket the asset metrics by @Bucket, but join market prices by matching the hour?
        // Yes, market prices are hourly.

        return await connection.QueryAsync<EnergyMixDto>(Queries.GetEnergyMix, new { Start = start, End = end, Bucket = bucket });
    }
    public async Task<IEnumerable<dynamic>> GetDeviceStatusAsync(CancellationToken ct)
    {
        using var connection = await dataSource.OpenConnectionAsync(ct);

        // Fetch devices and their LATEST metric (using DISTINCT ON for efficiency in Postgres)
        // Note: DISTINCT ON (asset_id) ORDER BY asset_id, time DESC gives the last row per asset.
        return await connection.QueryAsync<dynamic>(Queries.GetDeviceStatus);
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
}

public class DeviceStatusDto
{
    public string DeviceId { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? CurrentPowerKw { get; set; }
    public DateTimeOffset? LastUpdated { get; set; }
}

public class EnergyMixDto
{
    public DateTimeOffset Time { get; set; }
    public double? PowerKw { get; set; }
    public double? PricePerMwh { get; set; }
}
