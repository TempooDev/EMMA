using Dapper;
using EMMA.MarketService.Services;
using EMMA.Shared;
using Npgsql;

namespace EMMA.MarketService.Data;

public class MarketPriceRepository(NpgsqlDataSource dataSource, ILogger<MarketPriceRepository> logger)
{
    public async Task SavePricesAsync(IEnumerable<PricingValue> prices, string currency, string source, CancellationToken ct)
    {
        using var connection = await dataSource.OpenConnectionAsync(ct);

        foreach (var price in prices)
        {
            try
            {
                await connection.ExecuteAsync(Queries.InsertMarketPrice, new
                {
                    Time = price.Datetime.ToUniversalTime(),
                    Price = price.Value,
                    Currency = currency,
                    Source = source
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error inserting price for {Time}", price.Datetime);
            }
        }
    }

    public async Task SaveFlowAsync(DateTimeOffset time, string direction, double physicalFlow, double scheduledFlow, double ntc, double saturation, CancellationToken ct)
    {
        using var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            await connection.ExecuteAsync(Queries.InsertInterconnectionFlow, new
            {
                Time = time.ToUniversalTime(),
                Direction = direction,
                PhysicalFlow = physicalFlow,
                ScheduledFlow = scheduledFlow,
                Ntc = ntc,
                Saturation = saturation
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inserting flow for {Time}", time);
        }
    }
}
