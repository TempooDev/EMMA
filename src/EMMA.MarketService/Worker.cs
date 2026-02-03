using EMMA.MarketService.Services;
using EMMA.MarketService.Data; // Added Namespace

namespace EMMA.MarketService;

public class Worker(
    IServiceProvider serviceProvider,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Market Service Worker Started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<RedDataClient>();
                var alertService = scope.ServiceProvider.GetRequiredService<MarketAlertService>();
                var arbitrageService = scope.ServiceProvider.GetRequiredService<ArbitrageService>();
                var repository = scope.ServiceProvider.GetRequiredService<MarketPriceRepository>(); // Added Repo

                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                // Fetch Prices
                var prices = await client.GetHourlyPricesAsync("BZN|ES", stoppingToken);
                
                // Save Prices
                if (prices.Count > 0)
                {
                    logger.LogInformation("Fetched {Count} prices. Saving to DB...", prices.Count);
                    await repository.SavePricesAsync(prices, "EUR", "REData", stoppingToken);
                    
                    // Analyze Arbitrage
                    await arbitrageService.AnalyzeAsync("BZN|ES", prices, stoppingToken);
                }
                else
                {
                    logger.LogWarning("No prices fetched from API.");
                }

                var now = DateTimeOffset.UtcNow;
                
                foreach (var p in prices)
                {
                    // Filter: Alert for current or future prices.
                    if (p.Datetime >= now.AddHours(-1)) 
                    {
                        await alertService.EvaluatePriceAsync("BZN|ES", p, stoppingToken);
                    }
                }

                // --- Interconnection Monitoring ---
                logger.LogInformation("Checking ES-FR Interconnection Flows...");
                var entsoeClient = scope.ServiceProvider.GetRequiredService<EntsoeClient>();
                var interRepo = scope.ServiceProvider.GetRequiredService<InterconnectionRepository>();
                
                // ES (Spain) to FR (France)
                var flows = await entsoeClient.GetInterconnectionDataAsync("10YES-REE------0", "10YFR-RTE------C", stoppingToken);
                
                if (flows.Count > 0)
                {
                    await interRepo.SaveFlowsAsync(flows, stoppingToken);
                    
                    var currentFlow = flows.FirstOrDefault(f => f.AtTime >= now.AddHours(-1));
                    if (currentFlow != null)
                    {
                        logger.LogInformation("Interconnection Flow: {Flow}MW / {Cap}MW ({Saturation:F1}%)", 
                            currentFlow.PhysicalFlowMw, currentFlow.NtcMw, currentFlow.SaturationPercentage);
                        
                        if (currentFlow.SaturationPercentage > 90)
                        {
                            logger.LogWarning("ALERT: ES-FR Interconnection Saturated ({Saturation:F1}%). Price decoupling highly likely.", 
                                currentFlow.SaturationPercentage);
                        }
                    }
                }

                // Sleep for 1 hour
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Market Worker");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Retry sooner on error
            }
        }
    }
}
