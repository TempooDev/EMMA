using Dapper;
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

        var sql = @"
            WITH metric_data AS (
                SELECT time_bucket(@Bucket::interval, time) as bucket_time, AVG(power_kw) as avg_power
                FROM asset_metrics
                WHERE time BETWEEN @Start AND @End
                GROUP BY bucket_time
            ),
            price_data AS (
                SELECT time_bucket(@Bucket::interval, time) as bucket_time, AVG(price) as avg_price
                FROM market_prices
                WHERE time BETWEEN @Start AND @End
                GROUP BY bucket_time
            )
            SELECT 
                COALESCE(m.bucket_time, p.bucket_time) as Time,
                m.avg_power as PowerKw,
                p.avg_price as PricePerMwh
            FROM metric_data m
            FULL OUTER JOIN price_data p ON m.bucket_time = p.bucket_time
            ORDER BY Time ASC;
        ";

        return await connection.QueryAsync<EnergyMixDto>(sql, new { Start = start, End = end, Bucket = bucket });
    }
    public async Task<IEnumerable<DeviceStatusDto>> GetDeviceStatusAsync(CancellationToken ct)
    {
        using var connection = await dataSource.OpenConnectionAsync(ct);

        // Fetch devices and their LATEST metric (using DISTINCT ON for efficiency in Postgres)
        // Note: DISTINCT ON (asset_id) ORDER BY asset_id, time DESC gives the last row per asset.
        var sql = @"
            SELECT 
                d.device_id as DeviceId,
                d.latitude as Latitude,
                d.longitude as Longitude,
                m.power_kw as CurrentPowerKw,
                m.time as LastUpdated
            FROM devices d
            LEFT JOIN LATERAL (
                SELECT power_kw, time 
                FROM asset_metrics 
                WHERE asset_id = d.device_id 
                ORDER BY time DESC 
                LIMIT 1
            ) m ON true;
        ";

        return await connection.QueryAsync<DeviceStatusDto>(sql);
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
