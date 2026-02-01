using Dapper;
using Npgsql;

namespace EMMA.Server.Infrastructure.Data;

public class DbInitializer(NpgsqlDataSource dataSource, ILogger<DbInitializer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Waiting for database connection...");
            
            // Simple retry policy or wait logic could be added here, 
            // but Aspire service defaults usually handle resilience if configured,
            // or we rely on the container restart policy.
            // However, since we are doing this in a BackgroundService, the app starts.
            
            using var connection = await dataSource.OpenConnectionAsync(stoppingToken);

            logger.LogInformation("Initializing database schema...");

            var sql = @"
                -- 1. Table for device metadata
                CREATE TABLE IF NOT EXISTS public.devices (
                    device_id VARCHAR(50) PRIMARY KEY,
                    installation_date TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                    model_name VARCHAR(100) NOT NULL,
                    firmware_version VARCHAR(50),
                    latitude DOUBLE PRECISION,
                    longitude DOUBLE PRECISION
                );

                -- 2. Table for raw telemetry data (Time-series data)
                CREATE TABLE IF NOT EXISTS public.telemetry_raw (
                    time TIMESTAMP WITH TIME ZONE NOT NULL,
                    device_id VARCHAR(50) REFERENCES public.devices(device_id) ON DELETE CASCADE,
                    data_type VARCHAR(50) NOT NULL,
                    value DOUBLE PRECISION NOT NULL,
                    unit VARCHAR(10) NOT NULL
                );

                -- 3. Create the Hypertable (Requires TimescaleDB extension)
                SELECT create_hypertable('telemetry_raw', 'time', if_not_exists => TRUE);

                -- 4. Create index for faster querying by device_id and time
                CREATE INDEX IF NOT EXISTS telemetry_raw_device_time_idx ON public.telemetry_raw (device_id, time DESC);

                -- 5. Energy Communities
                CREATE TABLE IF NOT EXISTS energy_communities (
                    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    name TEXT NOT NULL,
                    description TEXT,
                    created_at TIMESTAMPTZ DEFAULT NOW(),
                    updated_at TIMESTAMPTZ DEFAULT NOW()
                );

                -- 6. Users
                CREATE TABLE IF NOT EXISTS users (
                    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    first_name TEXT NOT NULL,
                    last_name TEXT NOT NULL,
                    email TEXT UNIQUE NOT NULL,
                    created_at TIMESTAMPTZ DEFAULT NOW(),
                    updated_at TIMESTAMPTZ DEFAULT NOW()
                );

                -- 7. Assets (linked to community and/or user)
                CREATE TABLE IF NOT EXISTS assets (
                    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    name TEXT NOT NULL,
                    type TEXT NOT NULL,
                    description TEXT,
                    community_id INT REFERENCES energy_communities(id),
                    user_id INT REFERENCES users(id),
                    created_at TIMESTAMPTZ DEFAULT NOW(),
                    updated_at TIMESTAMPTZ DEFAULT NOW()
                );

                -- 8. Assets Metrics (Hypertable)
                CREATE TABLE IF NOT EXISTS assets_metrics (
                    time TIMESTAMPTZ NOT NULL,
                    asset_id INT REFERENCES assets(id),
                    metric_type TEXT NOT NULL,
                    value DOUBLE PRECISION NOT NULL,
                    unit TEXT,
                    PRIMARY KEY (time, asset_id, metric_type)
                );

                -- 9. Hypertable for assets_metrics
                SELECT create_hypertable('assets_metrics', 'time', if_not_exists => TRUE);

                -- 10. Compression Policy for assets_metrics
                -- Enable compression
                ALTER TABLE assets_metrics SET (
                    timescaledb.compress,
                    timescaledb.compress_segmentby = 'asset_id'
                );
                
                -- Add policy safely
                DO $$
                BEGIN
                    BEGIN
                        PERFORM add_compression_policy('assets_metrics', INTERVAL '30 days');
                    EXCEPTION WHEN OTHERS THEN
                        NULL; -- Policy likely already exists
                    END;
                END $$;

                -- 11. Market Prices
                CREATE TABLE IF NOT EXISTS market_prices (
                    time TIMESTAMPTZ NOT NULL,
                    price DOUBLE PRECISION NOT NULL,
                    currency TEXT NOT NULL,
                    source TEXT NOT NULL, -- e.g., 'ENTSO-E', 'REData'
                    PRIMARY KEY (time, source)
                );

                -- 12. Hypertable for market_prices
                SELECT create_hypertable('market_prices', 'time', if_not_exists => TRUE);
            ";

            await connection.ExecuteAsync(sql);

            logger.LogInformation("Database initialized successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing database");
            // Depending on requirements, we might want to kill the app if DB fails to init
            // but for now logging is sufficient.
        }
    }
}
