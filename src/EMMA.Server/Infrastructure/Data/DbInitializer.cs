using Dapper;
using EMMA.Shared;
using Npgsql;

namespace EMMA.Server.Infrastructure.Data;

public class DbInitializer(
    [FromKeyedServices("app-db")] NpgsqlDataSource appDataSource,
    [FromKeyedServices("telemetry-db")] NpgsqlDataSource telemetryDataSource,
    ILogger<DbInitializer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Waiting for database connections...");

            await InitializeDatabaseAsync(appDataSource, SchemaSql.AppScripts, "AppDB", stoppingToken);
            await InitializeDatabaseAsync(telemetryDataSource, SchemaSql.TelemetryScripts, "TelemetryDB", stoppingToken);

            // Special Post-Initialization for Telemetry (TimescaleDB Policies)
            await ApplyTelemetryPoliciesAsync(telemetryDataSource, stoppingToken);

            logger.LogInformation("All databases initialized successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing databases");
        }
    }

    private async Task InitializeDatabaseAsync(NpgsqlDataSource dataSource, Dictionary<string, string> scripts, string dbName, CancellationToken token)
    {
        logger.LogInformation("Initializing {DbName} schema...", dbName);
        using var connection = await dataSource.OpenConnectionAsync(token);

        foreach (var kvp in scripts)
        {
            try
            {
                await connection.ExecuteAsync(kvp.Value);
                logger.LogInformation("[{DbName}] Initialized: {Section}", dbName, kvp.Key);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{DbName}] Error initializing section: {Section}", dbName, kvp.Key);
            }
        }
    }

    private async Task ApplyTelemetryPoliciesAsync(NpgsqlDataSource dataSource, CancellationToken token)
    {
        using var connection = await dataSource.OpenConnectionAsync(token);
        try
        {
            logger.LogInformation("Applying TimescaleDB policies...");
            await connection.ExecuteAsync($"DO $$ BEGIN {SchemaSql.AssetMetricsCompression} EXCEPTION WHEN OTHERS THEN NULL; END $$;");
            await connection.ExecuteAsync($"DO $$ BEGIN {SchemaSql.AssetMetricsRetention} EXCEPTION WHEN OTHERS THEN NULL; END $$;");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error applying TimescaleDB policies (might already exist)");
        }
    }
}
