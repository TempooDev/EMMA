using EMMA.MarketService.Data; // Added Namespace
using EMMA.MarketService.Services;

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

                // --- Spanish Market Prices Only ---
                // Fetch Prices
                var prices = await client.GetHourlyPricesAsync("BZN|ES", stoppingToken);

                // Save Prices
                if (prices.Count > 0)
                {
                    logger.LogInformation("Fetched {Count} prices. Saving to DB...", prices.Count);
                    await repository.SavePricesAsync(prices, "EUR", "REData", stoppingToken);

                    // Analyze Arbitrage (Intraday focus)
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

                // --- Simulation: France Prices & Interconnection Flows ---
                // This enables the Arbitrage visualization.
                // France price is usually lower/higher than Spain depending on wind/solar.
                // Let's simulate a random variation from the Spanish price.
                var random = new Random();
                var frPrices = prices.Select(p => new PricingValue
                {
                    Datetime = p.Datetime,
                    Value = p.Value + (random.NextDouble() * 40 - 20) // +/- 20 EUR spread
                }).ToList();

                await repository.SavePricesAsync(frPrices, "EUR", "SIMULATED-FR", stoppingToken);

                // Simulate Flow: ES -> FR or FR -> ES
                await repository.SaveFlowAsync(
                    DateTimeOffset.UtcNow,
                    random.Next(2) == 0 ? "ES>FR" : "FR>ES",
                    random.NextDouble() * 2000, // 0-2000 MW physical
                    random.NextDouble() * 1800, // scheduled
                    2500, // NTC
                    random.NextDouble() * 100, // Saturation %
                    stoppingToken
                );

                logger.LogInformation("Simulated France prices and Interconnection flows for Arbitrage.");

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
