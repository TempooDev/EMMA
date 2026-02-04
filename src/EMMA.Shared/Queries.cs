namespace EMMA.Shared;

public static class Queries
{
    public const string GetEnergyMix = @"
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

    public const string GetDeviceStatus = @"
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

    public const string InsertMarketPrice = @"
        INSERT INTO market_prices (time, price, currency, source)
        VALUES (@Time, @Price, @Currency, @Source)
        ON CONFLICT (time, source) 
        DO UPDATE SET price = EXCLUDED.price, currency = EXCLUDED.currency;
    ";

    public const string InsertAssetMetric = @"
        INSERT INTO asset_metrics (time, asset_id, power_kw, energy_total_kwh, temperature)
        VALUES (@Time, @AssetId, @PowerKw, @EnergyTotalKwh, @Temperature)
        ON CONFLICT (time, asset_id) DO NOTHING;
    ";

    public const string InsertDevice = @"
        INSERT INTO public.devices (device_id, model_name, latitude, longitude, tenant_id, market_zone)
        VALUES (@DeviceId, @ModelName, @Latitude, @Longitude, @TenantId, @MarketZone)
        ON CONFLICT (device_id) DO UPDATE 
        SET model_name = EXCLUDED.model_name,
            latitude = EXCLUDED.latitude,
            longitude = EXCLUDED.longitude,
            tenant_id = EXCLUDED.tenant_id,
            market_zone = EXCLUDED.market_zone;
    ";
    
    public const string InsertInterconnectionFlow = @"
        INSERT INTO interconnection_flows (at_time, flow_direction, physical_flow_mw, scheduled_flow_mw, ntc_mw, saturation_percentage)
        VALUES (@Time, @Direction, @PhysicalFlow, @ScheduledFlow, @Ntc, @Saturation)
        ON CONFLICT (at_time, flow_direction) 
        DO UPDATE SET physical_flow_mw = EXCLUDED.physical_flow_mw, 
                      scheduled_flow_mw = EXCLUDED.scheduled_flow_mw,
                      ntc_mw = EXCLUDED.ntc_mw,
                      saturation_percentage = EXCLUDED.saturation_percentage;
    ";

    public const string SelectPendingEvents = "SELECT event_id FROM processed_messages WHERE event_id = ANY(@EventIds) FOR UPDATE SKIP LOCKED";

    public const string GetVppCapacityByZone = @"
        SELECT 
            d.market_zone as MarketZone,
            SUM(m.power_kw) as TotalPowerKw,
            COUNT(d.device_id) as DeviceCount
        FROM devices d
        INNER JOIN LATERAL (
            SELECT power_kw 
            FROM asset_metrics 
            WHERE asset_id = d.device_id 
            AND time > NOW() - INTERVAL '5 minutes'
            ORDER BY time DESC 
            LIMIT 1
        ) m ON true
        GROUP BY d.market_zone;
    ";

    public const string InsertFlexibilityBid = @"
        INSERT INTO flexibility_bids (market_zone, reduction_mw, price_per_mwh, status)
        VALUES (@MarketZone, @ReductionMw, @PricePerMwh, @Status);
    ";
}
