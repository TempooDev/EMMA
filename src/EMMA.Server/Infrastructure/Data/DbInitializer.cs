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

            var scripts = new Dictionary<string, string>
            {
                ["devices"] = @"
                    CREATE TABLE IF NOT EXISTS public.devices (
                        device_id VARCHAR(50) PRIMARY KEY,
                        installation_date TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                        model_name VARCHAR(100) NOT NULL,
                        firmware_version VARCHAR(50),
                        latitude DOUBLE PRECISION,
                        longitude DOUBLE PRECISION
                    );",

                ["raw_data_schema"] = "CREATE SCHEMA IF NOT EXISTS raw_data;",

                ["telemetry_raw"] = @"
                    CREATE TABLE IF NOT EXISTS raw_data.telemetry_raw (
                        time TIMESTAMP WITH TIME ZONE NOT NULL,
                        device_id VARCHAR(50) REFERENCES public.devices(device_id) ON DELETE CASCADE,
                        data_type VARCHAR(50) NOT NULL,
                        value DOUBLE PRECISION NOT NULL,
                        unit VARCHAR(10) NOT NULL
                    );
                    SELECT create_hypertable('raw_data.telemetry_raw', 'time', if_not_exists => TRUE);
                    CREATE INDEX IF NOT EXISTS telemetry_raw_device_time_idx ON raw_data.telemetry_raw (device_id, time DESC);",

                ["energy_communities"] = @"
                    CREATE TABLE IF NOT EXISTS energy_communities (
                        id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        name TEXT NOT NULL,
                        description TEXT,
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        updated_at TIMESTAMPTZ DEFAULT NOW()
                    );",

                ["users"] = @"
                    CREATE TABLE IF NOT EXISTS users (
                        id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        first_name TEXT NOT NULL,
                        last_name TEXT NOT NULL,
                        email TEXT UNIQUE NOT NULL,
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        updated_at TIMESTAMPTZ DEFAULT NOW()
                    );",

                ["assets"] = @"
                    CREATE TABLE IF NOT EXISTS assets (
                        id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        name TEXT NOT NULL,
                        type TEXT NOT NULL,
                        description TEXT,
                        community_id INT REFERENCES energy_communities(id),
                        user_id INT REFERENCES users(id),
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        updated_at TIMESTAMPTZ DEFAULT NOW()
                    );",

                ["asset_metrics"] = @"
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
                    );",
                 
                 ["market_prices"] = @"
                    CREATE TABLE IF NOT EXISTS market_prices (
                        time TIMESTAMPTZ NOT NULL,
                        price DOUBLE PRECISION NOT NULL,
                        currency TEXT NOT NULL,
                        source TEXT NOT NULL, 
                        PRIMARY KEY (time, source)
                    );
                    SELECT create_hypertable('market_prices', 'time', if_not_exists => TRUE);",

                 ["processed_messages"] = @"
                    CREATE TABLE IF NOT EXISTS processed_messages (
                        event_id UUID PRIMARY KEY,
                        processed_at TIMESTAMPTZ DEFAULT NOW(),
                        consumer_group TEXT
                    );"
            };

            foreach (var kvp in scripts)
            {
                try
                {
                    await connection.ExecuteAsync(kvp.Value);
                    logger.LogInformation("Initialized: {Section}", kvp.Key);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error initializing database section: {Section}", kvp.Key);
                    // Continue with other sections even if one fails
                }
            }

            // Compression Policy for asset_metrics (separately to ignore errors safely)
            try 
            {
                 await connection.ExecuteAsync("PERFORM add_compression_policy('asset_metrics', INTERVAL '30 days');");
            }
            catch {} // Ignore if exists or fails

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
