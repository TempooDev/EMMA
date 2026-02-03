namespace EMMA.Ingestion.Models;

public record AssetMetric(
    DateTimeOffset Time,
    string AssetId,
    double? Power,
    double? Energy,
    double? Temperature
);
