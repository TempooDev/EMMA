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
        INSERT INTO public.devices (device_id, model_name, latitude, longitude)
        VALUES (@DeviceId, @ModelName, @Latitude, @Longitude)
        ON CONFLICT (device_id) DO UPDATE 
        SET model_name = EXCLUDED.model_name,
            latitude = EXCLUDED.latitude,
            longitude = EXCLUDED.longitude;
    ";
    
    public const string SelectPendingEvents = "SELECT event_id FROM processed_messages WHERE event_id = ANY(@EventIds) FOR UPDATE SKIP LOCKED";
}
