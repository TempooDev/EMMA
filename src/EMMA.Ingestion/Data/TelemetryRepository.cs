using Dapper;
using EMMA.Ingestion.Extensions;
using EMMA.Ingestion.Models;
using Npgsql;
using Polly.Retry;

namespace EMMA.Ingestion.Data;

public class TelemetryRepository : ITelemetryRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<TelemetryRepository> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    private const string InsertSql = @"
        INSERT INTO asset_metrics (time, asset_id, power_kw, energy_total_kwh, temperature)
        VALUES (@Time, @AssetId, @Power, @Energy, @Temperature)
        ON CONFLICT (time, asset_id) DO NOTHING;";

    public TelemetryRepository(NpgsqlDataSource dataSource, ILogger<TelemetryRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
        _retryPolicy = ResilienceExtensions.CreateDbRetryPolicy(logger);
    }

    public async Task SaveMetricsAsync(IEnumerable<AssetMetric> metrics, CancellationToken token = default)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            using var connection = await _dataSource.OpenConnectionAsync(token);
            using var transaction = await connection.BeginTransactionAsync(token);
            
            try 
            {
                // Dapper executeAsync loops over the collection.
                // For thousands of items, this might be slower than COPY, 
                // but requirement was explicitly Dapper + INSERT ON CONFLICT.
                await connection.ExecuteAsync(InsertSql, metrics, transaction: transaction);
                await transaction.CommitAsync(token);
            }
            catch
            {
                // Rollback happens automatically on dispose if not committed
                throw;
            }
        });
    }
}
