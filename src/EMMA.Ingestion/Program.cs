using EMMA.Ingestion;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("emma-db");
builder.AddKafkaConsumer<string, string>("messaging", settings =>
{
    settings.Config.GroupId = "ingestion-group";
    settings.Config.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
