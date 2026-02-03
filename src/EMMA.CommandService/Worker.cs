using Confluent.Kafka;

namespace EMMA.CommandService;

public class Worker(DecisionMaker decisionMaker, IConfiguration config, ILogger<Worker> logger) : BackgroundService
{
    private const string AlertTopic = "market-alerts";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = config.GetConnectionString("messaging"),
            GroupId = "command-service-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(AlertTopic);

        logger.LogInformation("Subscribed to {Topic}", AlertTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    if (result != null)
                    {
                        logger.LogInformation("Received message on {Topic}: {Message}", AlertTopic, result.Message.Value);
                        await decisionMaker.ProcessAlertAsync(result.Message.Value, stoppingToken);
                    }
                }
                catch (ConsumeException e)
                {
                    logger.LogError(e, "Error consuming Kafka message");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        finally 
        {
            consumer.Close();
        }
    }
}
