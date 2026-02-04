using Dapper;
using Npgsql;

namespace EMMA.Api.Features.Market.Data;

public interface IMarketRepository
{
    Task<MarketSummaryEntity?> GetCurrentPriceAsync(CancellationToken ct = default);
    Task<bool> IsArbitrageActiveAsync(CancellationToken ct = default);
    Task<InterconnectionStatusEntity?> GetLatestInterconnectionStatusAsync(CancellationToken ct = default);
}

public record InterconnectionStatusEntity(double SaturationPercentage, string Direction);

public class MarketRepository([FromKeyedServices("telemetry-db")] NpgsqlDataSource dataSource) : IMarketRepository
{
    public async Task<MarketSummaryEntity?> GetCurrentPriceAsync(CancellationToken ct = default)
    {
        using var connection = await dataSource.OpenConnectionAsync(ct);
        const string query = @"
            SELECT 
                price as CurrentPrice,
                currency as Currency
            FROM market_prices
            WHERE time <= NOW() AND source = 'REData'
            ORDER BY time DESC
            LIMIT 1;";

        return await connection.QuerySingleOrDefaultAsync<MarketSummaryEntity>(query);
    }

    public async Task<bool> IsArbitrageActiveAsync(CancellationToken ct = default)
    {
        using var connection = await dataSource.OpenConnectionAsync(ct);
        const string query = @"
            WITH valid_prices AS (
                SELECT price FROM market_prices 
                WHERE time >= NOW() AND time < NOW() + INTERVAL '24 hours'
            )
            SELECT (MAX(price) - MIN(price)) > 50 as IsArbitrageActive
            FROM valid_prices;
        ";

        return await connection.ExecuteScalarAsync<bool>(query);
    }

    public async Task<InterconnectionStatusEntity?> GetLatestInterconnectionStatusAsync(CancellationToken ct = default)
    {
        using var connection = await dataSource.OpenConnectionAsync(ct);
        const string query = @"
            SELECT 
                saturation_percentage as SaturationPercentage,
                flow_direction as Direction
            FROM interconnection_flows
            ORDER BY at_time DESC
            LIMIT 1;";

        return await connection.QuerySingleOrDefaultAsync<InterconnectionStatusEntity>(query);
    }
}

public class MarketSummaryEntity
{
    public double CurrentPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
}
