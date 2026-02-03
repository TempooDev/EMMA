using EMMA.Api.Features.Market.Data;
using Mapster;

namespace EMMA.Api.Features.Market;

public interface IMarketService
{
    Task<MarketSummaryResponse> GetMarketSummaryAsync(CancellationToken ct = default);
}

public class MarketService(IMarketRepository repository) : IMarketService
{
    public async Task<MarketSummaryResponse> GetMarketSummaryAsync(CancellationToken ct = default)
    {
        var priceEntity = await repository.GetCurrentPriceAsync(ct);
        var isArbitrageActive = await repository.IsArbitrageActiveAsync(ct);
        var interconnection = await repository.GetLatestInterconnectionStatusAsync(ct);

        var response = priceEntity != null 
            ? priceEntity.Adapt<MarketSummaryResponse>() 
            : new MarketSummaryResponse { CurrentPrice = 0, Currency = "Unknown" };

        response.IsArbitrageActive = isArbitrageActive;
        
        if (interconnection != null)
        {
            response.InterconnectionSaturation = interconnection.SaturationPercentage;
            if (interconnection.SaturationPercentage > 90)
            {
                response.MarketWarning = $"Network Saturated ({interconnection.SaturationPercentage:F1}% on {interconnection.Direction}). Price decoupling highly likely.";
            }
        }

        return response;
    }
}

public class MarketSummaryResponse
{
    public double CurrentPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsArbitrageActive { get; set; }
    public double? InterconnectionSaturation { get; set; }
    public string? MarketWarning { get; set; }
}
