using Dapper;
using EMMA.MarketService.Services; // For PricingValue model
using Npgsql;

namespace EMMA.MarketService.Data;

public class MarketPriceRepository(NpgsqlDataSource dataSource, ILogger<MarketPriceRepository> logger)
{
    public async Task SavePricesAsync(IEnumerable<PricingValue> prices, string currency, string source, CancellationToken ct)
    {
        using var connection = await dataSource.OpenConnectionAsync(ct);
        using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            var sql = @"
                INSERT INTO market_prices (time, price, currency, source)
                VALUES (@Datetime, @Value, @Currency, @Source)
                ON CONFLICT (time, source) 
                DO UPDATE SET price = EXCLUDED.price, currency = EXCLUDED.currency;";

            // Map to flat object for Dapper
            var entities = prices.Select(p => new 
            {
                Datetime = p.Datetime.ToUniversalTime(),
                p.Value,
                Currency = currency,
                Source = source
            });

            await connection.ExecuteAsync(sql, entities, transaction);
            await transaction.CommitAsync(ct);
            
            logger.LogInformation("Saved {Count} prices from {Source}.", entities.Count(), source);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save prices");
            throw;
        }
    }
}
