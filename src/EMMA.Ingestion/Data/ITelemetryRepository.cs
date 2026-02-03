using EMMA.Ingestion.Models;

namespace EMMA.Ingestion.Data;

public interface ITelemetryRepository
{
    Task SaveMetricsAsync(IEnumerable<AssetMetric> metrics, CancellationToken token = default);
}
