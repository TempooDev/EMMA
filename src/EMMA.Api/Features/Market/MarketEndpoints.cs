using Dapper;
using Npgsql;

namespace EMMA.Api.Features.Market;

public static class MarketEndpoints
{
    public static void MapMarketEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/market/summary", async (NpgsqlDataSource dataSource) =>
        {
            using var connection = await dataSource.OpenConnectionAsync();
            
            // Get current price
            // Assuming we want the price for the current hour
            
            const string priceQuery = @"
                SELECT 
                    price as CurrentPrice,
                    currency as Currency
                FROM market_prices
                WHERE time <= NOW() AND source = 'REData'
                ORDER BY time DESC
                LIMIT 1;";

            var summary = await connection.QuerySingleOrDefaultAsync<MarketSummaryDto>(priceQuery);
            
            if (summary == null)
            {
                 summary = new MarketSummaryDto { CurrentPrice = 0, Currency = "Unknown" };
            }
            
            // Infer Arbitrage Mode (Spread > 50 in current day's forecast?)
            // We can re-use the daily logic or checking 'asset_metrics_hourly' isn't right.
            // We should check 'market_metrics' or just re-calculate spread for today.
            // Let's do a quick check on today's prices.
            
            const string arbitrageQuery = @"
                WITH valid_prices AS (
                    SELECT price FROM market_prices 
                    WHERE time >= NOW() AND time < NOW() + INTERVAL '24 hours'
                )
                SELECT (MAX(price) - MIN(price)) > 50 as IsArbitrageActive
                FROM valid_prices;
            ";
            
            var isArbitrage = await connection.ExecuteScalarAsync<bool>(arbitrageQuery);
            summary.IsArbitrageActive = isArbitrage;

            return Results.Ok(summary);
        })
        .WithName("GetMarketSummary");
    }
}

public class MarketSummaryDto
{
    public double CurrentPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsArbitrageActive { get; set; }
}
