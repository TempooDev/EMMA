using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace EMMA.CommandService.Tests;

public class DecisionMakerTests
{
    private readonly Mock<IProducer<string, string>> _mockProducer;
    private readonly Mock<ILogger<DecisionMaker>> _mockLogger;
    private readonly DecisionMaker _decisionMaker;

    public DecisionMakerTests()
    {
        _mockProducer = new Mock<IProducer<string, string>>();
        _mockLogger = new Mock<ILogger<DecisionMaker>>();
        _decisionMaker = new DecisionMaker(_mockProducer.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessAlertAsync_NegativePrice_SendsCommand()
    {
        // Arrange
        var alert = new
        {
            alert_type = "NEGATIVE_PRICE",
            zone = "ES",
            price = -10.0,
            currency = "EUR"
        };
        var json = JsonSerializer.Serialize(alert);

        // Act
        await _decisionMaker.ProcessAlertAsync(json, CancellationToken.None);

        // Assert
        _mockProducer.Verify(p => p.ProduceAsync(
            "asset-commands",
            It.Is<Message<string, string>>(m => 
                m.Value.Contains("START_CHARGING") && 
                m.Value.Contains("target_assets")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task ProcessAlertAsync_Cooldown_PreventsSecondCommand()
    {
        // Arrange
        var alert = new
        {
            alert_type = "NEGATIVE_PRICE",
            zone = "ES",
            price = -5.0
        };
        var json = JsonSerializer.Serialize(alert);

        // Act
        await _decisionMaker.ProcessAlertAsync(json, CancellationToken.None); // First trigger
        await _decisionMaker.ProcessAlertAsync(json, CancellationToken.None); // Second trigger (immediate)

        // Assert
        // Should only be called once due to cooldown
        _mockProducer.Verify(p => p.ProduceAsync(
            "asset-commands",
            It.IsAny<Message<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAlertAsync_OtherAlert_DoesNothing()
    {
        // Arrange
        var alert = new
        {
            alert_type = "HIGH_PRICE",
            zone = "ES",
            price = 200.0
        };
        var json = JsonSerializer.Serialize(alert);

        // Act
        await _decisionMaker.ProcessAlertAsync(json, CancellationToken.None);

        // Assert
        _mockProducer.Verify(p => p.ProduceAsync(
            It.IsAny<string>(),
            It.IsAny<Message<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
