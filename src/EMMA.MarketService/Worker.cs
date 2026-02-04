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
