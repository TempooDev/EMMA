namespace EMMA.Shared;

public static class Queries
{
    public const string GetEnergyMix = @"
        WITH metric_data AS (
            SELECT time_bucket(@Bucket::interval, m.time) as bucket_time, AVG(m.power_kw) as avg_power
            FROM asset_metrics m
            WHERE m.asset_id = ANY(@DeviceIds)
            AND m.time BETWEEN @Start AND @End
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
        WITH current_price AS (
            SELECT price 
            FROM market_prices 
            WHERE source = 'REData' 
            ORDER BY time DESC 
            LIMIT 1
        )
        SELECT 
            d.device_id as DeviceId,
            d.model_name as ModelName,
            d.latitude as Latitude,
            d.longitude as Longitude,
            m.power_kw as CurrentPowerKw,
            m.temperature as Temperature,
            m.time as LastUpdated,
            (m.power_kw > 0 AND (SELECT price FROM current_price) <= 0) as IsChargingNegativePrice
        FROM devices d
        LEFT JOIN LATERAL (
            -- This part is for information purposes. Caller must ensure devices are from the same DB 
            -- or this query should be split. For GetDeviceStatus, we'll likely fetch metrics separately.
            SELECT power_kw, temperature, time 
            FROM asset_metrics 
            WHERE asset_id = d.device_id 
            ORDER BY time DESC 
            LIMIT 1
        ) m ON true
        WHERE d.tenant_id = @TenantId;
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
            asset_id as AssetId,
            power_kw as PowerKw
        FROM asset_metrics
        WHERE time > NOW() - INTERVAL '5 minutes'
        ORDER BY time DESC;
    ";

    public const string InsertFlexibilityBid = @"
        INSERT INTO flexibility_bids (market_zone, reduction_mw, price_per_mwh, status)
        VALUES (@MarketZone, @ReductionMw, @PricePerMwh, @Status);
    ";

    public const string GetCrossBorderArbitrage = @"
        WITH current_prices AS (
            SELECT DISTINCT ON (source) source, price, time
            FROM market_prices
            WHERE source IN ('REData', 'SIMULATED-FR')
            AND time > NOW() - INTERVAL '4 hours'
            ORDER BY source, time DESC
        ),
        current_flow AS (
            SELECT physical_flow_mw, scheduled_flow_mw, ntc_mw, saturation_percentage, flow_direction, at_time
            FROM interconnection_flows
            WHERE at_time > NOW() - INTERVAL '2 hours'
            ORDER BY at_time DESC
            LIMIT 1
        )
        SELECT 
            (SELECT price FROM current_prices WHERE source = 'REData') as PriceEs,
            (SELECT price FROM current_prices WHERE source = 'SIMULATED-FR') as PriceFr,
            f.physical_flow_mw as PhysicalFlowMw,
            f.ntc_mw as NtcMw,
            f.saturation_percentage as SaturationPercentage,
            f.flow_direction as FlowDirection
        FROM current_flow f;
    ";

    public const string GetImpactMetrics = @"
        WITH day_prices AS (
            SELECT AVG(price) as avg_price
            FROM market_prices
            WHERE time >= CURRENT_DATE
            AND source = 'REData'
        ),
        current_price AS (
            SELECT price 
            FROM market_prices 
            WHERE source = 'REData' 
            ORDER BY time DESC 
            LIMIT 1
        ),
        today_metrics AS (
            SELECT 
                SUM(m.power_kw * (5.0/3600.0)) as total_kwh,
                SUM(CASE WHEN p.price < 0 THEN m.power_kw * (5.0/3600.0) ELSE 0 END) as negative_price_kwh
            FROM asset_metrics m
            LEFT JOIN market_prices p ON time_bucket('5 seconds', m.time) = time_bucket('5 seconds', p.time) AND p.source = 'REData'
            WHERE m.time >= CURRENT_DATE
            AND m.asset_id = ANY(@DeviceIds)
        )
        SELECT 
            COALESCE((SELECT avg_price FROM day_prices) - (SELECT price FROM current_price), 0) * COALESCE((SELECT total_kwh FROM today_metrics), 0) / 1000.0 as TotalSavingsEur,
            COALESCE((SELECT negative_price_kwh FROM today_metrics), 0) as NegativePriceEnergyKwh,
            COALESCE((SELECT price FROM current_price), 0) as CurrentPriceEurMwh;
    ";

    public const string GetPriceForecast = @"
        SELECT 
            time as Time,
            price as PricePerMwh
        FROM market_prices
        WHERE source = 'REData'
        AND time >= NOW()
        AND time <= NOW() + INTERVAL '24 hours'
        ORDER BY time ASC;
    ";
}
