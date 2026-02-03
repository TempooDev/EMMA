using System.Xml.Linq;
using EMMA.MarketService.Data;

namespace EMMA.MarketService.Services;

public class EntsoeClient(IConfiguration configuration, ILogger<EntsoeClient> logger)
{
    private readonly string _apiKey = configuration["Entsoe:ApiKey"] ?? throw new InvalidOperationException("Entsoe:ApiKey is missing.");
    private const string BaseUrl = "https://web-api.tp.entsoe.eu/api";

    public async Task<List<InterconnectionFlow>> GetInterconnectionDataAsync(string fromArea, string toArea, CancellationToken ct)
    {
        var flows = new List<InterconnectionFlow>();
        
        // In a real scenario, we would call two different document types:
        // A25: Scheduled Commercial Exchanges
        // A26: Physical Flows
        
        // For this implementation, we will simulate the ENTSO-E response parsing 
        // to stay within the requested logic of "saturation monitor".
        
        try
        {
            // Simulate fetching and parsing
            // DocumentType A25 (Scheduled)
            // DocumentType A26 (Physical)
            
            // Re-use logic for ES-FR border
            var now = DateTime.UtcNow;
            var start = now.AddHours(-1);
            var end = now.AddHours(24);

            // This is where we would normally do the HTTP GET
            // var url = $"{BaseUrl}?securityToken={_apiKey}&documentType=A25&in_Domain={toArea}&out_Domain={fromArea}&periodStart={start:yyyyMMddHHmm}&periodEnd={end:yyyyMMddHHmm}";
            
            // Mocking the result for the demo to show the "saturation" logic
            for (int i = 0; i < 24; i++)
            {
                var time = DateTime.UtcNow.Date.AddHours(DateTime.UtcNow.Hour + i);
                
                // Simulate a saturated cable (95% saturation) for some hours
                bool isSaturated = i < 4; 
                double ntc = 3000; // 3000 MW capacity ES-FR approx
                double physical = isSaturated ? 2850 : 1500; 
                double scheduled = isSaturated ? 2800 : 1400;

                flows.Add(new InterconnectionFlow
                {
                    AtTime = time,
                    Direction = $"{fromArea}-{toArea}",
                    PhysicalFlowMw = physical,
                    ScheduledFlowMw = scheduled,
                    NtcMw = ntc
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching data from ENTSO-E");
        }

        return flows;
    }
}
