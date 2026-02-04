using Dapper;
using EMMA.Server.Infrastructure.Identity;
using EMMA.Shared;
using Npgsql;

namespace EMMA.Server.Infrastructure.Data;

public class DashboardRepository(
    [FromKeyedServices("app-db")] NpgsqlDataSource appDataSource,
    [FromKeyedServices("telemetry-db")] NpgsqlDataSource telemetryDataSource,
    ITenantProvider tenantProvider)
{
    public async Task<IEnumerable<EnergyMixDto>> GetEnergyMixAsync(DateTimeOffset start, DateTimeOffset end, string bucket, CancellationToken ct)
    {
        var tenantId = tenantProvider.TenantId;

        // 1. Fetch authorized device IDs from AppDB
        using var appConn = await appDataSource.OpenConnectionAsync(ct);
        var deviceIds = (await appConn.QueryAsync<string>("SELECT device_id FROM devices WHERE tenant_id = @TenantId", new { TenantId = tenantId })).ToArray();

        if (deviceIds.Length == 0) return Enumerable.Empty<EnergyMixDto>();

        // 2. Fetch bucketed data from TelemetryDB
        using var teleConn = await telemetryDataSource.OpenConnectionAsync(ct);

        var allowedBuckets = new HashSet<string> { "5 seconds", "30 seconds", "1 minute", "5 minutes", "15 minutes", "30 minutes", "1 hour", "1 day" };
        if (!allowedBuckets.Contains(bucket))
        {
            bucket = "1 minute"; // Fallback default
        }

        return await teleConn.QueryAsync<EnergyMixDto>(Queries.GetEnergyMix, new
        {
            Start = start,
            End = end,
            Bucket = bucket,
            DeviceIds = deviceIds
        });
    }

    public async Task<IEnumerable<DeviceStatusDto>> GetDeviceStatusAsync(CancellationToken ct)
    {
        var tenantId = tenantProvider.TenantId;

        // 1. Fetch devices from AppDB
        using var appConn = await appDataSource.OpenConnectionAsync(ct);
        var devices = (await appConn.QueryAsync<DeviceStatusDto>(
            "SELECT device_id as DeviceId, model_name as ModelName, latitude as Latitude, longitude as Longitude FROM devices WHERE tenant_id = @TenantId",
            new { TenantId = tenantId })).ToList();

        if (!devices.Any()) return devices;

        // 2. Fetch latest metrics for these devices from TelemetryDB
        using var teleConn = await telemetryDataSource.OpenConnectionAsync(ct);
        var deviceIds = devices.Select(d => d.DeviceId).ToArray();

        var metrics = (await teleConn.QueryAsync<dynamic>(@"
            SELECT DISTINCT ON (asset_id) asset_id, power_kw, temperature, time 
            FROM asset_metrics 
            WHERE asset_id = ANY(@Ids) 
            ORDER BY asset_id, time DESC", new { Ids = deviceIds }))
            .ToDictionary(m => (string)m.asset_id, m => m);

        var currentPrice = await teleConn.ExecuteScalarAsync<double?>(
            "SELECT price FROM market_prices WHERE source = 'REData' ORDER BY time DESC LIMIT 1");

        foreach (var d in devices)
        {
            if (metrics.TryGetValue(d.DeviceId, out var m))
            {
                d.CurrentPowerKw = (double?)m.power_kw;
                d.Temperature = (double?)m.temperature;
                d.LastUpdated = (DateTimeOffset)m.time;
                d.IsChargingNegativePrice = d.CurrentPowerKw > 0 && (currentPrice ?? 1) <= 0;
            }
        }

        return devices;
    }

    public async Task<IEnumerable<dynamic>> GetVppCapacityByZoneAsync(CancellationToken cts = default)
    {
        // 1. Fetch all devices and their zones from AppDB
        using var appConn = await appDataSource.OpenConnectionAsync(cts);
        var deviceZones = (await appConn.QueryAsync<dynamic>("SELECT device_id, market_zone FROM devices")).ToList();

        if (!deviceZones.Any()) return Enumerable.Empty<dynamic>();

        // 2. Fetch latest power data from TelemetryDB for the fetched device IDs
        using var teleConn = await telemetryDataSource.OpenConnectionAsync(cts);
        var deviceIds = deviceZones.Select(d => (string)d.device_id).ToArray();
        var metrics = (await teleConn.QueryAsync<dynamic>(Queries.GetVppCapacityByZone, new { DeviceIds = deviceIds }))
            .ToDictionary(m => (string)m.asset_id, m => (double)m.power_kw);

        // 3. Aggregate in-memory
        return deviceZones
            .GroupBy(d => (string)d.market_zone)
            .Select(g => new
            {
                MarketZone = g.Key,
                TotalPowerKw = g.Sum(d => metrics.GetValueOrDefault((string)d.device_id, 0.0)),
                DeviceCount = g.Count()
            });
    }

    public async Task InsertFlexibilityBidAsync(string marketZone, double reductionMw, double? pricePerMwh, string status = "SUBMITTED", CancellationToken cts = default)
    {
        // flexibility_bids is in TelemetryDB as it's high volume event data
        using var connection = await telemetryDataSource.OpenConnectionAsync(cts);
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
        using var connection = await telemetryDataSource.OpenConnectionAsync(cts);
        return await connection.QueryFirstOrDefaultAsync<ArbitrageDto>(Queries.GetCrossBorderArbitrage);
    }

    public async Task<ImpactMetricsDto?> GetImpactMetricsAsync(CancellationToken cts = default)
    {
        var tenantId = tenantProvider.TenantId;

        // 1. Fetch device IDs from AppDB
        using var appConn = await appDataSource.OpenConnectionAsync(cts);
        var deviceIds = (await appConn.QueryAsync<string>("SELECT device_id FROM devices WHERE tenant_id = @TenantId", new { TenantId = tenantId })).ToArray();

        if (deviceIds.Length == 0) return new ImpactMetricsDto();

        // 2. Fetch calculated metrics from TelemetryDB
        using var teleConn = await telemetryDataSource.OpenConnectionAsync(cts);
        return await teleConn.QueryFirstOrDefaultAsync<ImpactMetricsDto>(Queries.GetImpactMetrics, new { DeviceIds = deviceIds });
    }

    public async Task<IEnumerable<PriceForecastDto>> GetPriceForecastAsync(CancellationToken cts = default)
    {
        using var connection = await telemetryDataSource.OpenConnectionAsync(cts);
        return await connection.QueryAsync<PriceForecastDto>(Queries.GetPriceForecast);
    }
}

public class PriceForecastDto
{
    public DateTimeOffset Time { get; set; }
    public double PricePerMwh { get; set; }
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
