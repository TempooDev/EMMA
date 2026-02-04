using Dapper;
using EMMA.Shared;
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
                ["devices"] = SchemaSql.Devices,
                ["raw_data_schema"] = SchemaSql.RawDataSchema,
                ["telemetry_raw"] = SchemaSql.TelemetryRaw,
                ["energy_communities"] = SchemaSql.EnergyCommunities,
                ["users"] = SchemaSql.Users,
                ["assets"] = SchemaSql.Assets,
                ["asset_metrics"] = SchemaSql.AssetMetrics,
                ["market_prices"] = SchemaSql.MarketPrices,
                ["processed_messages"] = SchemaSql.ProcessedMessages,
                ["asset_mappings"] = SchemaSql.AssetMappings,
                ["asset_metrics_hourly"] = SchemaSql.AssetMetricsHourly,
                ["asset_metrics_daily"] = SchemaSql.AssetMetricsDaily,
                ["audit_logs"] = SchemaSql.AuditLogs,
                ["interconnection_flows"] = SchemaSql.InterconnectionFlows,
                ["optimization_schedules"] = SchemaSql.OptimizationSchedules,
                ["api_keys"] = SchemaSql.ApiKeys,
                ["flexibility_bids"] = SchemaSql.FlexibilityBids
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
                    logger.LogError(ex, "Error initializing database schema section: {Section}", kvp.Key);
                    // Continue with other sections even if one fails
                }
            }

            // Fix existing table to add market_zone (for legacy databases)
            await connection.ExecuteAsync("ALTER TABLE devices ADD COLUMN IF NOT EXISTS market_zone VARCHAR(50) DEFAULT 'Iberica-ES'");

            // Compression Policy for asset_metrics (separately to ignore errors safely)
            try
            {
                await connection.ExecuteAsync($"DO $$ BEGIN {SchemaSql.AssetMetricsCompression} EXCEPTION WHEN OTHERS THEN NULL; END $$;");
                await connection.ExecuteAsync($"DO $$ BEGIN {SchemaSql.AssetMetricsRetention} EXCEPTION WHEN OTHERS THEN NULL; END $$;");
            }
            catch { } // Ignore if exists or fails
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
