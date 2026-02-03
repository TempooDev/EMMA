using EMMA.CommandService;
using Confluent.Kafka;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var kafkaConfig = new ProducerConfig { BootstrapServers = builder.Configuration.GetConnectionString("messaging") };

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(kafkaConfig).Build());

builder.Services.AddSingleton<DecisionMaker>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
