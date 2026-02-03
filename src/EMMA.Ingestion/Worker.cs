using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Confluent.Kafka;
using Dapper;
using EMMA.Ingestion.Data; // Added
using EMMA.Ingestion.Models; // Added
using EMMA.Shared;
using Npgsql;
using NpgsqlTypes;
using Polly;
using Polly.Retry;

namespace EMMA.Ingestion;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConsumer<string, string> _consumer;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ITelemetryRepository _repository; // Added
    private const string Topic = "telemetry-raw";
    private const int BatchSize = 100;
    private const int ChannelCapacity = 1000;
    private static readonly TimeSpan BatchTimeout = TimeSpan.FromSeconds(5);

    public Worker(ILogger<Worker> logger, IConsumer<string, string> consumer, NpgsqlDataSource dataSource, ITelemetryRepository repository)
    {
        _logger = logger;
        _consumer = consumer;
        _dataSource = dataSource;
        _repository = repository;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var channel = Channel.CreateBounded<ConsumeResult<string, string>>(new BoundedChannelOptions(ChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        var consumerTask = Task.Run(() => ConsumeLoop(channel.Writer, stoppingToken), stoppingToken);
        var processorTask = ProcessLoop(channel.Reader, stoppingToken);

        await Task.WhenAny(consumerTask, processorTask);
    }

    private async Task ConsumeLoop(ChannelWriter<ConsumeResult<string, string>> writer, CancellationToken token)
    {
        _consumer.Subscribe(Topic);
        _logger.LogInformation("Subscribed to topic {Topic}", Topic);

        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(TimeSpan.FromMilliseconds(100));
                    if (result != null)
                    {
                        await writer.WriteAsync(result, token);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in consume loop");
                    await Task.Delay(1000, token);
                }
            }
        }
        finally
        {
            _consumer.Close();
            writer.TryComplete();
        }
    }

    private async Task ProcessLoop(ChannelReader<ConsumeResult<string, string>> reader, CancellationToken token)
    {
        var batch = new List<ConsumeResult<string, string>>(BatchSize);

        while (await reader.WaitToReadAsync(token))
        {
            batch.Clear();
            var timer = Stopwatch.StartNew();

            while (batch.Count < BatchSize && timer.Elapsed < BatchTimeout)
            {
                if (reader.TryRead(out var item))
                {
                    batch.Add(item);
                }
                else
                {
                    if (batch.Count > 0)
                    {
                        var remaining = BatchTimeout - timer.Elapsed;
                        if (remaining <= TimeSpan.Zero) break;

                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                        cts.CancelAfter(remaining);
                        try
                        {
                            if (!await reader.WaitToReadAsync(cts.Token)) break;
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (batch.Count > 0)
            {
                await ProcessBatchAsync(batch, token);
            }
        }
    }

    private async Task ProcessBatchAsync(List<ConsumeResult<string, string>> batch, CancellationToken token)
    {
        using var logScope = _logger.BeginScope("Batch processing count={Count}", batch.Count);

        var parsedMessages = new List<(Guid EventId, string TechnicalId, DateTimeOffset Timestamp, double? Latitude, double? Longitude, JsonElement Root)>(batch.Count);

        foreach (var item in batch)
        {
            try
            {
                using var doc = JsonDocument.Parse(item.Message.Value);
                var root = doc.RootElement.Clone();
                if (TryExtractMetadata(root, out var eventId, out var technicalId, out var timestamp, out var latitude, out var longitude))
                {
                    parsedMessages.Add((eventId, technicalId, timestamp, latitude, longitude, root));
                }
            }
            catch (JsonException)
            {
                _logger.LogWarning("Skipping invalid JSON message");
            }
        }

        if (parsedMessages.Count == 0) return;

        try
        {
            using var connection = await _dataSource.OpenConnectionAsync(token);
            using var transaction = await connection.BeginTransactionAsync(token);

            // Privacy: Get or Create Anonymous IDs
            var technicalIds = parsedMessages.Select(m => m.TechnicalId).Distinct().ToList();
            var mappings = await GetAssetMappingsAsync(connection, technicalIds, transaction, token);

            // Idempotency: Filter duplicates by event_id
            var eventIds = parsedMessages.Select(m => m.EventId).ToArray();
            var existingIds = await connection.QueryAsync<Guid>(
                Queries.SelectPendingEvents,
                new { EventIds = eventIds },
                transaction: transaction);

            var existingIdSet = existingIds.ToHashSet();
            var newMessages = parsedMessages.Where(m => !existingIdSet.Contains(m.EventId)).ToList();

            if (newMessages.Count > 0)
            {
                // 1. Upsert Device Info (using anonymous ID)
                var distinctDevices = newMessages
                    .GroupBy(m => m.TechnicalId)
                    .Select(g => new
                    {
                        TechnicalId = g.Key,
                        AnonymousId = mappings[g.Key],
                        Latitude = g.FirstOrDefault(x => x.Latitude.HasValue).Latitude,
                        Longitude = g.FirstOrDefault(x => x.Longitude.HasValue).Longitude
                    })
                    .ToList();

                var devicesPayload = distinctDevices.Select(d => new
                {
                    DeviceId = d.AnonymousId.ToString(), // Use Anonymous ID for devices table
                    ModelName = "Anonymized Asset",
                    d.Latitude,
                    d.Longitude
                });

                await connection.ExecuteAsync(Queries.InsertDevice,
                    devicesPayload,
                    transaction: transaction);

                // 2. Persist Metrics via Repository
                var metrics = new List<AssetMetric>(newMessages.Count);
                foreach (var msg in newMessages)
                {
                    if (msg.Root.TryGetProperty("measurements", out var measurements))
                    {
                        double? power = null, energy = null, temp = null;
                        if (measurements.TryGetProperty("power_kw", out var pElem) && pElem.ValueKind == JsonValueKind.Number) power = pElem.GetDouble();
                        if (measurements.TryGetProperty("energy_total_kwh", out var eElem) && eElem.ValueKind == JsonValueKind.Number) energy = eElem.GetDouble();
                        if (measurements.TryGetProperty("inverter_temp_c", out var tElem) && tElem.ValueKind == JsonValueKind.Number) temp = tElem.GetDouble();

                        // Use the anonymous ID in metrics
                        metrics.Add(new AssetMetric(msg.Timestamp, mappings[msg.TechnicalId].ToString(), power, energy, temp));
                    }

                    // Privacy: Mask logs
                    _logger.LogInformation("Processed message {EventId} for Asset [MASKED:{Hash}]", msg.EventId, MaskId(msg.TechnicalId));
                }

                await _repository.SaveMetricsAsync(metrics, token);

                // 3. Mark as Processed (Idempotency) - COPY
                using (var writer = await connection.BeginBinaryImportAsync(
                    "COPY processed_messages (event_id, consumer_group, processed_at) FROM STDIN (FORMAT BINARY)"))
                {
                    foreach (var msg in newMessages)
                    {
                        await writer.StartRowAsync(token);
                        await writer.WriteAsync(msg.EventId, NpgsqlDbType.Uuid, token);
                        await writer.WriteAsync("ingestion-group", NpgsqlDbType.Text, token);
                        await writer.WriteAsync(DateTime.UtcNow, NpgsqlDbType.TimestampTz, token);
                    }
                    await writer.CompleteAsync(token);
                }

                await transaction.CommitAsync(token);
                _logger.LogInformation("Batch processed. {Count} messages inserted using anonymous IDs.", newMessages.Count);
            }
            else
            {
                await transaction.CommitAsync(token);
                _logger.LogInformation("All duplicates.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process batch");
        }
    }

    private async Task<Dictionary<string, Guid>> GetAssetMappingsAsync(NpgsqlConnection conn, List<string> technicalIds, NpgsqlTransaction transaction, CancellationToken ct)
    {
        var existing = (await conn.QueryAsync<(string TechnicalId, Guid AnonymousId)>(
            "SELECT technical_id, anonymous_id FROM asset_mappings WHERE technical_id = ANY(@Ids)",
            new { Ids = technicalIds },
            transaction: transaction)).ToDictionary(x => x.TechnicalId, x => x.AnonymousId);

        var missing = technicalIds.Where(id => !existing.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            foreach (var id in missing)
            {
                var anonId = Guid.NewGuid();
                await conn.ExecuteAsync(
                    "INSERT INTO asset_mappings (technical_id, anonymous_id) VALUES (@Tech, @Anon) ON CONFLICT (technical_id) DO NOTHING",
                    new { Tech = id, Anon = anonId },
                    transaction: transaction);
                existing[id] = anonId;
            }
        }

        return existing;
    }

    private static string MaskId(string id) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id))).Substring(0, 8);


    private static bool TryExtractMetadata(JsonElement root, out Guid eventId, out string assetId, out DateTimeOffset timestamp, out double? latitude, out double? longitude)
    {
        eventId = Guid.Empty;
        assetId = string.Empty;
        timestamp = default;
        latitude = null;
        longitude = null;

        if (!root.TryGetProperty("header", out var header) ||
            !header.TryGetProperty("event_id", out var eventIdElem) ||
            !header.TryGetProperty("asset_id", out var assetIdElem))
        {
            return false;
        }

        if (!Guid.TryParse(eventIdElem.GetString(), out eventId)) return false;

        assetId = assetIdElem.GetString() ?? string.Empty;
        if (string.IsNullOrEmpty(assetId)) return false;

        if (!root.TryGetProperty("timestamp", out var timestampElem) ||
            !DateTimeOffset.TryParse(timestampElem.GetString(), out timestamp))
        {
            return false;
        }

        if (root.TryGetProperty("location", out var location))
        {
            if (location.TryGetProperty("latitude", out var latElem) && latElem.ValueKind == JsonValueKind.Number)
                latitude = latElem.GetDouble();

            if (location.TryGetProperty("longitude", out var lonElem) && lonElem.ValueKind == JsonValueKind.Number)
                longitude = lonElem.GetDouble();
        }

        return true;
    }
}
