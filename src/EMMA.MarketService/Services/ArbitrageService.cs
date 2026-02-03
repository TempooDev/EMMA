using System.Text.Json;
using Confluent.Kafka;
using EMMA.MarketService.Services; // Ensure PricingValue is visible

namespace EMMA.MarketService.Services;

public class ArbitrageService(IProducer<string, string> producer, ILogger<ArbitrageService> logger)
{
    private const string Topic = "market-alerts";
    private const double MinSpread = 50.0; // EUR/MWh

    public async Task AnalyzeAsync(string zone, List<PricingValue> prices, CancellationToken ct)
    {
        if (prices == null || prices.Count < 2) return;

        // Find min and max in the future window
        var now = DateTimeOffset.UtcNow;
        var futurePrices = prices.Where(p => p.Datetime >= now).OrderBy(p => p.Datetime).ToList();

        if (futurePrices.Count == 0) return;

        var minPrice = futurePrices.MinBy(p => p.Value);
        var maxPrice = futurePrices.MaxBy(p => p.Value);

        if (minPrice == null || maxPrice == null) return;

        // Simple logic: If we can buy at min and sell at max (later or even earlier if storage allowed, 
        // but typically we charge first then discharge. 
        // However, for pure arbitrage opportunity detection, we just look for the spread in the window.)
        
        // Let's assume strict temporal order: Charge (Min) -> Discharge (Max)
        // If Max is before Min, we might look for another pair or just report the spread for grid stability.
        // Requirement: "Best time to charge vs Best time to discharge based on 24h forecast". 
        // It doesn't strictly say discharge MUST be after charge in this iteration, but it makes physical sense.
        
        // Let's refine: Find best spread where Discharge > Charge time.
        // Actually, for a daily cycle, we might charge at night (low) and discharge at peak (high).
        // Let's stick to the simplest interpretation first: Global Min and Global Max in the window.
        
        var spread = maxPrice.Value - minPrice.Value;

        if (spread > MinSpread)
        {
            logger.LogInformation("Arbitrage Opportunity found! Spread: {Spread} EUR/MWh. Charge: {Min}, Discharge: {Max}", 
                spread, minPrice.Value, maxPrice.Value);

            var opportunity = new
            {
                alert_type = "ARBITRAGE_OPPORTUNITY",
                zone,
                spread,
                best_charge_at = minPrice.Datetime,
                best_discharge_at = maxPrice.Datetime,
                min_price = minPrice.Value,
                max_price = maxPrice.Value
            };

            var json = JsonSerializer.Serialize(opportunity);

            await producer.ProduceAsync(Topic, new Message<string, string>
            {
                Key = zone,
                Value = json
            }, ct);
        }
    }
}
