using EMMA.Api.Data.Repositories;
using Mapster;

namespace EMMA.Api.Services;

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

        var response = priceEntity != null 
            ? priceEntity.Adapt<MarketSummaryResponse>() 
            : new MarketSummaryResponse { CurrentPrice = 0, Currency = "Unknown" };

        response.IsArbitrageActive = isArbitrageActive;
        return response;
    }
}

public class MarketSummaryResponse
{
    public double CurrentPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsArbitrageActive { get; set; }
}
