using Confluent.Kafka;
using EMMA.MarketService.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace EMMA.MarketService.Tests;

public class ArbitrageServiceTests
{
    private readonly Mock<IProducer<string, string>> _mockProducer;
    private readonly Mock<ILogger<ArbitrageService>> _mockLogger;
    private readonly ArbitrageService _arbitrageService;

    public ArbitrageServiceTests()
    {
        _mockProducer = new Mock<IProducer<string, string>>();
        _mockLogger = new Mock<ILogger<ArbitrageService>>();
        _arbitrageService = new ArbitrageService(_mockProducer.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task AnalyzeAsync_HighSpread_PublishesOpportunity()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var prices = new List<PricingValue>
        {
            new() { Value = 10.0, Datetime = now.AddHours(1) }, // Charge here
            new() { Value = 70.0, Datetime = now.AddHours(5) }  // Discharge here (Spread 60 > 50)
        };

        // Act
        await _arbitrageService.AnalyzeAsync("ES", prices, CancellationToken.None);

        // Assert
        _mockProducer.Verify(p => p.ProduceAsync(
            "market-alerts",
            It.Is<Message<string, string>>(m => 
                m.Value.Contains("ARBITRAGE_OPPORTUNITY") && 
                m.Value.Contains("spread\":60") &&
                m.Value.Contains("min_price\":10")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeAsync_LowSpread_DoesNotPublish()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var prices = new List<PricingValue>
        {
            new() { Value = 10.0, Datetime = now.AddHours(1) },
            new() { Value = 40.0, Datetime = now.AddHours(5) }  // Spread 30 < 50
        };

        // Act
        await _arbitrageService.AnalyzeAsync("ES", prices, CancellationToken.None);

        // Assert
        _mockProducer.Verify(p => p.ProduceAsync(
            It.IsAny<string>(),
            It.IsAny<Message<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnalyzeAsync_NoFuturePrices_DoesNotPublish()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var prices = new List<PricingValue>
        {
            new() { Value = 10.0, Datetime = now.AddHours(-5) }, // Past
            new() { Value = 90.0, Datetime = now.AddHours(-1) }  // Past
        };

        // Act
        await _arbitrageService.AnalyzeAsync("ES", prices, CancellationToken.None);

        // Assert
        _mockProducer.Verify(p => p.ProduceAsync(
            It.IsAny<string>(),
            It.IsAny<Message<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
