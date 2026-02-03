using Dapper;
using EMMA.Ingestion.Extensions;
using EMMA.Ingestion.Models;
using global::EMMA.Shared;
using Npgsql;
using Polly.Retry;

namespace EMMA.Ingestion.Data;

public class TelemetryRepository : ITelemetryRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<TelemetryRepository> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

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

                var entities = metrics.Select(m => new
                {
                    m.Time,
                    m.AssetId,
                    PowerKw = m.Power,
                    EnergyTotalKwh = m.Energy,
                    m.Temperature
                });

                await connection.ExecuteAsync(Queries.InsertAssetMetric, entities, transaction: transaction);
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
