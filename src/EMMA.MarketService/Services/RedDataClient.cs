using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EMMA.MarketService.Services;

public class RedDataClient(HttpClient httpClient, ILogger<RedDataClient> logger)
{
    public async Task<List<PricingValue>> GetHourlyPricesAsync(string zone, CancellationToken cancellationToken)
    {
        // REData API: /mercados/precios-mercados-tiempo-real
        // Zone mapping: BZN|ES is implicit for this endpoint usually, or check "geo_ids".
        // REData usually returns PVPC for Spain by default.
        // We will fetch for the current day.
        
        var now = DateTime.UtcNow;
        var start = now.Date.ToString("yyyy-MM-ddTHH:mm");
        var end = now.Date.AddDays(1).AddHours(23).ToString("yyyy-MM-ddTHH:mm");

        var url = $"/es/datos/mercados/precios-mercados-tiempo-real?start_date={start}&end_date={end}&time_trunc=hour";
        
        logger.LogInformation("Fetching prices from REData: {Url}", url);

        try 
        {
            var response = await httpClient.GetFromJsonAsync<RedDataResponse>(url, cancellationToken);
            
            if (response?.Included == null) 
            {
                logger.LogWarning("REData Response is null or 'included' is missing.");
                return [];
            }

            logger.LogInformation("REData Response received. Included items: {Count}", response.Included.Count);

            var pvpc = response.Included.FirstOrDefault(x => x.Attributes?.Title?.Contains("Precio", StringComparison.OrdinalIgnoreCase) == true);
            
            if (pvpc == null)
            {
                logger.LogWarning("Could not find 'Precio' in REData response. Titles found: {Titles}", 
                    string.Join(", ", response.Included.Select(x => x.Attributes?.Title ?? "null")));
            }

            return pvpc?.Attributes?.Values ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking REData API");
            return [];
        }
    }
}

// Models
public class RedDataResponse
{
    [JsonPropertyName("included")]
    public List<IncludedItem> Included { get; set; } = [];
}

public class IncludedItem
{
    [JsonPropertyName("attributes")]
    public Attributes? Attributes { get; set; }
}

public class Attributes
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    
    [JsonPropertyName("values")]
    public List<PricingValue> Values { get; set; } = [];
}

public class PricingValue
{
    [JsonPropertyName("value")]
    public double Value { get; set; }
    
    [JsonPropertyName("datetime")]
    public DateTimeOffset Datetime { get; set; }
}
