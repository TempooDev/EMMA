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

        var parsedMessages = new List<(Guid EventId, string AssetId, DateTimeOffset Timestamp, double? Latitude, double? Longitude, JsonElement Root)>(batch.Count);

        foreach (var item in batch)
        {
            try
            {
                using var doc = JsonDocument.Parse(item.Message.Value);
                var root = doc.RootElement.Clone();
                if (TryExtractMetadata(root, out var eventId, out var assetId, out var timestamp, out var latitude, out var longitude))
                {
                    parsedMessages.Add((eventId, assetId, timestamp, latitude, longitude, root));
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
                // 1. Upsert Device Info (Location etc)
                var distinctDevices = newMessages
                    .GroupBy(m => m.AssetId)
                    .Select(g => new
                    {
                        DeviceId = g.Key,
                        Latitude = g.FirstOrDefault(x => x.Latitude.HasValue).Latitude,
                        Longitude = g.FirstOrDefault(x => x.Longitude.HasValue).Longitude
                    })
                    .ToList();

                var devicesPayload = distinctDevices.Select(d => new
                {
                    d.DeviceId,
                    ModelName = "Unknown Model",
                    d.Latitude,
                    d.Longitude
                });

                await connection.ExecuteAsync(Queries.InsertDevice,
                    devicesPayload,
                    transaction: transaction);

                // 2. Persist Metrics via Repository (Replacing Refined Table Insert)
                // Map to AssetMetric
                var metrics = new List<AssetMetric>(newMessages.Count);
                foreach (var msg in newMessages)
                {
                    if (msg.Root.TryGetProperty("measurements", out var measurements))
                    {
                        double? power = null;
                        double? energy = null;
                        double? temp = null;

                        if (measurements.TryGetProperty("power_kw", out var pElem) && pElem.ValueKind == JsonValueKind.Number)
                            power = pElem.GetDouble();

                        if (measurements.TryGetProperty("energy_total_kwh", out var eElem) && eElem.ValueKind == JsonValueKind.Number)
                            energy = eElem.GetDouble();

                        if (measurements.TryGetProperty("inverter_temp_c", out var tElem) && tElem.ValueKind == JsonValueKind.Number)
                            temp = tElem.GetDouble();

                        metrics.Add(new AssetMetric(msg.Timestamp, msg.AssetId, power, energy, temp));
                    }
                }

                // Call Repository used for Issue #6 (Poly resilient Dapper INSERT)
                // Note: We are using a separate connection managed by Repository internal retry policy?
                // Actually repository uses _dataSource.OpenConnectionAsync.
                // WE ARE IN A TRANSACTION HERE for processed_messages and devices.
                // The repository opens its OWN connection/transaction.
                // This breaks the single atomic transaction assurance for the whole batch including `processed_messages`.
                // However, `processed_messages` and `devices` uses `transaction` created above.
                // `SaveMetricsAsync` uses its own connection.
                // The requirement asked for "Resilience (Polly)... handle transient database connection failures".
                // If we pass the transaction to the repository, we can't easily retry the whole transaction inside the repository without re-running the outer logic.
                // So separating them (eventual consistency) is acceptable given the need for Retry Policy on the Metric Insert.
                // OR we move everything to Repository?
                // For now, I will call repository here. If metrics fail, we might successfully mark valid IDs as processed?
                // Check flow:
                // If repository fails (after retries), we throw. 
                // The outer exception catch logs it. The transaction (devices/processed) is NOT committed because `transaction.CommitAsync` is at the end.
                // So if repository throws, we rollback `devices` and `processed_messages`. Correct.
                // BUT, repository actions are already committed?
                // Repository `SaveMetricsAsync` uses `ExecuteAsync` ... and `BeginTransactionAsync`. It commits internally.
                // So if repository succeeds but then `processed_messages` fails (rare), we have metrics but no event_id record. 
                // That's acceptable (at-least-once).
                // If `processed_messages` succeeds but repository fails -> Exception -> rollback processed -> consistent (nothing saved).
                // Wait. `processed_messages` insert is BELOW.

                await _repository.SaveMetricsAsync(metrics, token);

                // 3. Mark as Processed (Idempotency) - COPY
                // Only if metrics saved successfully (or partially if we don't care about partials inside repository).
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
                _logger.LogInformation("Batch processed. {Count} messages inserted.", newMessages.Count);
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
