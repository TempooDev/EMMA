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
                -- create_hypertable throws if it exists unless we use if_not_exists => TRUE
                SELECT create_hypertable('telemetry_raw', 'time', if_not_exists => TRUE);

                -- 4. Create index for faster querying by device_id and time
                CREATE INDEX IF NOT EXISTS telemetry_raw_device_time_idx ON public.telemetry_raw (device_id, time DESC);
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
