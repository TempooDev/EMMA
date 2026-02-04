using EMMA.Ingestion;
using EMMA.Ingestion.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("telemetry-db");
builder.AddKafkaConsumer<string, string>("messaging", settings =>
{
    settings.Config.GroupId = "ingestion-group";
    settings.Config.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;
});

builder.Services.AddSingleton<EMMA.Ingestion.Data.ITelemetryRepository, EMMA.Ingestion.Data.TelemetryRepository>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
