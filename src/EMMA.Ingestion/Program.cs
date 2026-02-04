using EMMA.Ingestion;
using EMMA.Ingestion.Data;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Dapper / Npgsql
builder.Services.AddKeyedSingleton<NpgsqlDataSource>("app-db", (sp, key) =>
{
    var connectionString = builder.Configuration.GetConnectionString("app-db")
        ?? throw new InvalidOperationException("Connection string 'app-db' is missing.");
    return NpgsqlDataSource.Create(connectionString);
});

builder.Services.AddKeyedSingleton<NpgsqlDataSource>("telemetry-db", (sp, key) =>
{
    var connectionString = builder.Configuration.GetConnectionString("telemetry-db")
        ?? throw new InvalidOperationException("Connection string 'telemetry-db' is missing.");
    return NpgsqlDataSource.Create(connectionString);
});

builder.AddKafkaConsumer<string, string>("messaging", settings =>
{
    settings.Config.GroupId = "ingestion-group";
    settings.Config.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;
});

builder.Services.AddSingleton<ITelemetryRepository, TelemetryRepository>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
