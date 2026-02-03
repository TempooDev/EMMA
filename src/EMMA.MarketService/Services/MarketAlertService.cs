using System.Text.Json;
using Confluent.Kafka;

namespace EMMA.MarketService.Services;

public class MarketAlertService(IProducer<string, string> producer, ILogger<MarketAlertService> logger)
{
    private const string Topic = "market-alerts";
    private const double LowThreshold = 0.0;
    private const double HighThreshold = 120.0;

    public async Task EvaluatePriceAsync(string zone, PricingValue priceData, CancellationToken ct)
    {
        bool isNegative = priceData.Value < LowThreshold;
        bool isHigh = priceData.Value > HighThreshold;

        if (isNegative || isHigh)
        {
            var alertType = isNegative ? "NEGATIVE_PRICE" : "HIGH_PRICE";
            
            // Assume the block is 1 hour
            var startsAt = priceData.Datetime;
            var endsAt = priceData.Datetime.AddHours(1);

            var alert = new
            {
                alert_type = alertType,
                zone,
                price = priceData.Value,
                currency = "EUR",
                starts_at = startsAt.ToUniversalTime(),
                ends_at = endsAt.ToUniversalTime()
            };

            var json = JsonSerializer.Serialize(alert);
            
            logger.LogInformation("Emitting Alert: {Json}", json);

            await producer.ProduceAsync(Topic, new Message<string, string> 
            { 
                Key = zone, 
                Value = json 
            }, ct);
        }
    }
}
