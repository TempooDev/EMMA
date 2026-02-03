namespace EMMA.Shared;

public static class SchemaSql
{
    public const string Devices = @"
        CREATE TABLE IF NOT EXISTS public.devices (
            device_id VARCHAR(50) PRIMARY KEY,
            installation_date TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            model_name VARCHAR(100) NOT NULL,
            firmware_version VARCHAR(50),
            latitude DOUBLE PRECISION,
            longitude DOUBLE PRECISION,
            tenant_id VARCHAR(50)
        );";

    public const string RawDataSchema = "CREATE SCHEMA IF NOT EXISTS raw_data;";

    public const string TelemetryRaw = @"
        CREATE TABLE IF NOT EXISTS raw_data.telemetry_raw (
            time TIMESTAMP WITH TIME ZONE NOT NULL,
            device_id VARCHAR(50) REFERENCES public.devices(device_id) ON DELETE CASCADE,
            data_type VARCHAR(50) NOT NULL,
            value DOUBLE PRECISION NOT NULL,
            unit VARCHAR(10) NOT NULL
        );
        SELECT create_hypertable('raw_data.telemetry_raw', 'time', if_not_exists => TRUE);
        CREATE INDEX IF NOT EXISTS telemetry_raw_device_time_idx ON raw_data.telemetry_raw (device_id, time DESC);";

    public const string EnergyCommunities = @"
        CREATE TABLE IF NOT EXISTS energy_communities (
            id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            name TEXT NOT NULL,
            description TEXT,
            created_at TIMESTAMPTZ DEFAULT NOW(),
            updated_at TIMESTAMPTZ DEFAULT NOW()
        );";

    public const string Users = @"
        CREATE TABLE IF NOT EXISTS users (
            id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            first_name TEXT NOT NULL,
            last_name TEXT NOT NULL,
            email TEXT UNIQUE NOT NULL,
            created_at TIMESTAMPTZ DEFAULT NOW(),
            updated_at TIMESTAMPTZ DEFAULT NOW()
        );";

    public const string Assets = @"
        CREATE TABLE IF NOT EXISTS assets (
            id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            name TEXT NOT NULL,
            type TEXT NOT NULL,
            description TEXT,
            community_id INT REFERENCES energy_communities(id),
            user_id INT REFERENCES users(id),
            created_at TIMESTAMPTZ DEFAULT NOW(),
            updated_at TIMESTAMPTZ DEFAULT NOW()
        );";

    public const string AssetMetrics = @"
        CREATE TABLE IF NOT EXISTS asset_metrics (
            time TIMESTAMPTZ NOT NULL,
            asset_id VARCHAR(50) NOT NULL,
            power_kw DOUBLE PRECISION,
            energy_total_kwh DOUBLE PRECISION,
            temperature DOUBLE PRECISION
        );
        SELECT create_hypertable('asset_metrics', 'time', if_not_exists => TRUE);
        CREATE UNIQUE INDEX IF NOT EXISTS asset_metrics_time_asset_id_idx ON asset_metrics (time, asset_id);
        CREATE INDEX IF NOT EXISTS asset_metrics_asset_time_idx ON asset_metrics (asset_id, time DESC);
        ALTER TABLE asset_metrics SET (
            timescaledb.compress,
            timescaledb.compress_segmentby = 'asset_id'
        );";

    public const string MarketPrices = @"
        CREATE TABLE IF NOT EXISTS market_prices (
            time TIMESTAMPTZ NOT NULL,
            price DOUBLE PRECISION NOT NULL,
            currency TEXT NOT NULL,
            source TEXT NOT NULL, 
            PRIMARY KEY (time, source)
        );
        SELECT create_hypertable('market_prices', 'time', if_not_exists => TRUE);";

    public const string ProcessedMessages = @"
        CREATE TABLE IF NOT EXISTS processed_messages (
            event_id UUID PRIMARY KEY,
            processed_at TIMESTAMPTZ DEFAULT NOW(),
            consumer_group TEXT
        );";

    public const string AssetMetricsCompression = "PERFORM add_compression_policy('asset_metrics', INTERVAL '30 days');";
    public const string AssetMetricsRetention = "PERFORM add_retention_policy('asset_metrics', INTERVAL '90 days');";

    public const string AssetMappings = @"
        CREATE TABLE IF NOT EXISTS asset_mappings (
            technical_id VARCHAR(50) PRIMARY KEY,
            anonymous_id UUID UNIQUE NOT NULL DEFAULT gen_random_uuid(),
            tenant_id VARCHAR(50)
        );";

    public const string AssetMetricsHourly = @"
        CREATE MATERIALIZED VIEW IF NOT EXISTS asset_metrics_hourly
        WITH (timescaledb.continuous) AS
        SELECT
            time_bucket('1 hour', time) AS bucket,
            asset_id,
            MIN(power_kw) AS min_power,
            MAX(power_kw) AS max_power,
            AVG(power_kw) AS avg_power,
            MAX(energy_total_kwh) - MIN(energy_total_kwh) AS total_energy_kwh
        FROM asset_metrics
        GROUP BY bucket, asset_id;

        SELECT add_continuous_aggregate_policy('asset_metrics_hourly',
            start_offset => INTERVAL '1 month',
            end_offset => INTERVAL '1 hour',
            schedule_interval => INTERVAL '10 minutes',
            if_not_exists => TRUE);";

    public const string AssetMetricsDaily = @"
        CREATE MATERIALIZED VIEW IF NOT EXISTS asset_metrics_daily
        WITH (timescaledb.continuous) AS
        SELECT
            time_bucket('1 day', time) AS bucket,
            asset_id,
            MIN(power_kw) AS min_power,
            MAX(power_kw) AS max_power,
            AVG(power_kw) AS avg_power,
            MAX(energy_total_kwh) - MIN(energy_total_kwh) AS total_energy_kwh
        FROM asset_metrics
        GROUP BY bucket, asset_id;

        SELECT add_continuous_aggregate_policy('asset_metrics_daily',
            start_offset => INTERVAL '1 year',
            end_offset => INTERVAL '1 day',
            schedule_interval => INTERVAL '1 hour',
            if_not_exists => TRUE);";
}
