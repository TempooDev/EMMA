using Confluent.Kafka;
using EMMA.MarketService;
using EMMA.MarketService.Data; // Added Namespace
using EMMA.MarketService.Services;
using Polly;
using Polly.Extensions.Http;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("telemetry-db");

// Polly Policy
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

// Register RedData Client
builder.Services.AddHttpClient<RedDataClient>(client =>
{
    client.BaseAddress = new Uri("https://apidatos.ree.es");
})
.AddPolicyHandler(retryPolicy);

// Register Kafka Producer
var kafkaConfig = new ProducerConfig { BootstrapServers = builder.Configuration.GetConnectionString("messaging") };
builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(kafkaConfig).Build());

// Services
builder.Services.AddSingleton<MarketAlertService>();
builder.Services.AddSingleton<ArbitrageService>();
builder.Services.AddSingleton<MarketPriceRepository>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
