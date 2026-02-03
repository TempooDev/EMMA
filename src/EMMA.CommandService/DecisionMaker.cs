using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace EMMA.CommandService;

public class DecisionMaker(IProducer<string, string> producer, ILogger<DecisionMaker> logger)
{
    private const string CommandTopic = "asset-commands";
    private const string NegativePriceAlert = "NEGATIVE_PRICE";
    private const string ArbitrageAlert = "ARBITRAGE_OPPORTUNITY";

    private DateTimeOffset _lastCommandTime = DateTimeOffset.MinValue;
    private readonly TimeSpan _cooldownCallback = TimeSpan.FromMinutes(10); // Cooldown period

    public async Task ProcessAlertAsync(string message, CancellationToken ct)
    {
        try 
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (root.TryGetProperty("alert_type", out var alertTypeProp))
            {
                var alertType = alertTypeProp.GetString();

                if (alertType == NegativePriceAlert)
                {
                    await HandleNegativePriceAsync(ct);
                }
                else if (alertType == ArbitrageAlert)
                {
                    await HandleArbitrageAsync(root, ct);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing alert message");
        }
    }

    private async Task HandleNegativePriceAsync(CancellationToken ct)
    {
        if (DateTimeOffset.UtcNow - _lastCommandTime < _cooldownCallback)
        {
            logger.LogInformation("Cooldown active. Skipping command generation.");
            return;
        }

        logger.LogInformation("Received NEGATIVE_PRICE alert. Sending START_CHARGING command.");

        var command = new
        {
            command = "START_CHARGING",
            target_assets = new[] { "all" },
            timestamp = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(command);

        await producer.ProduceAsync(CommandTopic, new Message<string, string>
        {
            Key = "broadcast",
            Value = json
        }, ct);

        _lastCommandTime = DateTimeOffset.UtcNow;
        logger.LogInformation("Command sent: {Json}", json);
    }

    private Task HandleArbitrageAsync(JsonElement root, CancellationToken ct)
    {
        // For Arbitrage, we currently just log the opportunity as per DoD.
        // DoD: "The system logs a 'Planned Arbitrage Action' when it detects a price spread > 50 €/MWh"
        
        var spread = root.GetProperty("spread").GetDouble();
        var chargeAt = root.GetProperty("best_charge_at").GetDateTimeOffset();
        var dischargeAt = root.GetProperty("best_discharge_at").GetDateTimeOffset();
        var minPrice = root.GetProperty("min_price").GetDouble();
        var maxPrice = root.GetProperty("max_price").GetDouble();

        logger.LogInformation(@"Planned Arbitrage Action:
            Spread: {Spread} EUR
            Best Charge Time: {ChargeAt} (Price: {MinPrice})
            Best Discharge Time: {DischargeAt} (Price: {MaxPrice})", 
            spread, chargeAt, dischargeAt, minPrice, maxPrice);

        return Task.CompletedTask;
    }
}
