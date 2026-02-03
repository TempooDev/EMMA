using Dapper;
using Npgsql;
using EMMA.Shared;

namespace EMMA.MarketService.Data;

public class InterconnectionRepository(NpgsqlDataSource dataSource, ILogger<InterconnectionRepository> logger)
{
    public async Task SaveFlowsAsync(IEnumerable<InterconnectionFlow> flows, CancellationToken ct)
    {
        using var connection = await dataSource.OpenConnectionAsync(ct);
        
        foreach (var flow in flows)
        {
            try 
            {
               await connection.ExecuteAsync(Queries.InsertInterconnectionFlow, new 
               {
                   Time = flow.AtTime.ToUniversalTime(),
                   Direction = flow.Direction,
                   PhysicalFlow = flow.PhysicalFlowMw,
                   ScheduledFlow = flow.ScheduledFlowMw,
                   Ntc = flow.NtcMw,
                   Saturation = flow.SaturationPercentage
               });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error inserting interconnection flow for {Time} {Direction}", flow.AtTime, flow.Direction);
            }
        }
    }
}

public class InterconnectionFlow
{
    public DateTime AtTime { get; set; }
    public string Direction { get; set; } = string.Empty;
    public double? PhysicalFlowMw { get; set; }
    public double? ScheduledFlowMw { get; set; }
    public double? NtcMw { get; set; }
    public double SaturationPercentage => (NtcMw.HasValue && NtcMw.Value > 0) 
        ? (PhysicalFlowMw ?? 0) / NtcMw.Value * 100 
        : 0;
}
