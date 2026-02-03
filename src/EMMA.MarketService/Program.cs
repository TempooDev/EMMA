using Confluent.Kafka;
using EMMA.MarketService;
using EMMA.MarketService.Services;
using EMMA.MarketService.Data; // Added Namespace
using Polly;
using Polly.Extensions.Http;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("emma-db");

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

builder.Services.AddHttpClient<EntsoeClient>()
    .AddPolicyHandler(retryPolicy);

// Services
builder.Services.AddSingleton<MarketAlertService>();
builder.Services.AddSingleton<ArbitrageService>();
builder.Services.AddSingleton<MarketPriceRepository>();
builder.Services.AddSingleton<InterconnectionRepository>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
