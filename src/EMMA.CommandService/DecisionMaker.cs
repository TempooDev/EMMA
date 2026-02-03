using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace EMMA.CommandService;

public class DecisionMaker(IProducer<string, string> producer, ILogger<DecisionMaker> logger)
{
    private const string CommandTopic = "asset-commands";
    private const string NegativePriceAlert = "NEGATIVE_PRICE";
    private DateTimeOffset _lastCommandTime = DateTimeOffset.MinValue;
    private readonly TimeSpan _cooldownCallback = TimeSpan.FromMinutes(10); // Cooldown period

    public async Task ProcessAlertAsync(string message, CancellationToken ct)
    {
        try 
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (root.TryGetProperty("alert_type", out var alertType) && 
                alertType.GetString() == NegativePriceAlert)
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing alert message");
        }
    }
}
