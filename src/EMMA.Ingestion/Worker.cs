using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Confluent.Kafka;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace EMMA.Ingestion;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConsumer<string, string> _consumer;
    private readonly NpgsqlDataSource _dataSource;
    private const string Topic = "telemetry-raw";
    private const int BatchSize = 100;
    private const int ChannelCapacity = 1000;
    private static readonly TimeSpan BatchTimeout = TimeSpan.FromSeconds(5);

    public Worker(ILogger<Worker> logger, IConsumer<string, string> consumer, NpgsqlDataSource dataSource)
    {
        _logger = logger;
        _consumer = consumer;
        _dataSource = dataSource;
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

            // Idempotency
            var eventIds = parsedMessages.Select(m => m.EventId).ToArray();
            var existingIds = await connection.QueryAsync<Guid>(
                "SELECT event_id FROM processed_messages WHERE event_id = ANY(@EventIds) FOR UPDATE SKIP LOCKED",
                new { EventIds = eventIds },
                transaction: transaction);

            var existingIdSet = existingIds.ToHashSet();
            var newMessages = parsedMessages.Where(m => !existingIdSet.Contains(m.EventId)).ToList();

            if (newMessages.Count > 0)
            {
                // Devices (Upsert Latitude/Longitude)
                var distinctDevices = newMessages
                    .GroupBy(m => m.AssetId)
                    .Select(g => new 
                    { 
                        DeviceId = g.Key, 
                        // Take first non-null location in batch
                        Latitude = g.FirstOrDefault(x => x.Latitude.HasValue).Latitude,
                        Longitude = g.FirstOrDefault(x => x.Longitude.HasValue).Longitude
                    })
                    .ToList();

                await connection.ExecuteAsync(@"
                    INSERT INTO public.devices (device_id, model_name, latitude, longitude)
                    VALUES (@DeviceId, 'Unknown Model', @Latitude, @Longitude)
                    ON CONFLICT (device_id) DO UPDATE 
                    SET latitude = COALESCE(EXCLUDED.latitude, devices.latitude),
                        longitude = COALESCE(EXCLUDED.longitude, devices.longitude);",
                    distinctDevices,
                    transaction: transaction);

                // Telemetry
                using (var writer = await connection.BeginBinaryImportAsync(
                    "COPY raw_data.telemetry_raw (time, device_id, data_type, value, unit) FROM STDIN (FORMAT BINARY)"))
                {
                    foreach (var msg in newMessages)
                    {
                        if (msg.Root.TryGetProperty("measurements", out var measurements))
                        {
                            foreach (var property in measurements.EnumerateObject())
                            {
                                if (property.Value.ValueKind == JsonValueKind.Number)
                                {
                                    var val = property.Value.GetDouble();
                                    var type = property.Name;
                                    var unit = type.Contains('_') ? type.Split('_').Last() : "unknown";

                                    await writer.StartRowAsync(token);
                                    await writer.WriteAsync(msg.Timestamp.UtcDateTime, NpgsqlDbType.TimestampTz, token);
                                    await writer.WriteAsync(msg.AssetId, NpgsqlDbType.Varchar, token);
                                    await writer.WriteAsync(type, NpgsqlDbType.Varchar, token);
                                    await writer.WriteAsync(val, NpgsqlDbType.Double, token);
                                    await writer.WriteAsync(unit, NpgsqlDbType.Varchar, token);
                                }
                            }
                        }
                    }
                    await writer.CompleteAsync(token);
                }

                // Processed Messages
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
