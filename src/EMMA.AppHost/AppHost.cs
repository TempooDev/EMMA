var builder = DistributedApplication.CreateBuilder(args);

var compose = builder.AddDockerComposeEnvironment("compose").WithDashboard(dashboard =>
{
  dashboard.WithHostPort(8080)
    .WithForwardedHeaders(enabled: true);
});

var postgresql = builder.AddPostgres("postgresql")
  .WithImage("timescale/timescaledb", "latest-pg17")
  .WithPgAdmin();

var identityDb = postgresql.AddDatabase("identity-db");
var appDb = postgresql.AddDatabase("app-db");
var telemetryDb = postgresql.AddDatabase("telemetry-db");

var kafka = builder.AddKafka("messaging")
  .PublishAsDockerComposeService((_, service) => { service.Name = "messaging"; })
  .WithKafkaUI();

var jwtKey = builder.AddParameter("jwt-key", secret: true);

var mqttBridge = builder.AddDockerfile("mqtt-bridge", "../simple-mqtt-kafka-bridge")
  .WithReference(kafka)
  .WaitFor(kafka)
  .WithEnvironment("KAFKA_BROKERS", $"{kafka.Resource.Name}:9093")
  .WithEnvironment("KAFKA_TOPIC", "telemetry-raw")
  .WithEndpoint(targetPort: 1883, name: "mqtt-port")
  .PublishAsDockerComposeService((_, service) => { service.Name = "mqtt-bridge"; });

var simulator = builder.AddPythonApp("energy-simulator", "../energy-simulator", "main.py")
    .WithEnvironment("MQTT_BROKER_URL", mqttBridge.GetEndpoint("mqtt-port"))
    .WithEnvironment("TENANT_ID", "T001")
    .WaitFor(mqttBridge)
    .PublishAsDockerComposeService((_, service) => { service.Name = "energy-simulator"; })
  ;

var server = builder.AddProject<Projects.EMMA_Server>("server")
  .WithHttpHealthCheck("/health")
  .WithExternalHttpEndpoints()
  .WithReference(appDb)
  .WithReference(telemetryDb)
  .WithEnvironment("Jwt__Key", jwtKey)
  .WithEnvironment("Jwt__Issuer", "emma-identity")
  .WithEnvironment("Jwt__Audience", "emma-api")
  .WaitFor(appDb)
  .WaitFor(telemetryDb)
  .PublishAsDockerComposeService((_, service) => { service.Name = "server"; });

var marketService = builder.AddProject<Projects.EMMA_MarketService>("market-service")
  .WithReference(kafka)
  .WithReference(telemetryDb)
  .WaitFor(kafka)
  .WaitFor(server)
  .PublishAsDockerComposeService((_, service) => { service.Name = "market-service"; });


var ingestion = builder.AddProject<Projects.EMMA_Ingestion>("ingestion")
  .WithReference(appDb)
  .WithReference(telemetryDb)
  .WithReference(kafka)
  .WaitFor(appDb)
  .WaitFor(telemetryDb)
  .WaitFor(kafka)
  .WaitFor(server)
  .PublishAsDockerComposeService((_, service) => { service.Name = "ingestion"; });

var identity = builder.AddDockerfile("emma-identity", "../..", "src/Emma.Identity/Dockerfile")
  .WithHttpEndpoint(targetPort: 8080, name: "http")
  .WithReference(identityDb)
  .WithEnvironment("Jwt__Key", jwtKey)
  .WithEnvironment("Jwt__Issuer", "emma-identity")
  .WithEnvironment("Jwt__Audience", "emma-api")
  .WaitFor(identityDb)
  .PublishAsDockerComposeService((_, service) => { service.Name = "emma-identity"; });

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
  .WithReference(server)
  .WithReference(identity.GetEndpoint("http"))
  .WithEnvironment("SERVER_HTTP", server.GetEndpoint("http"))
  .WithEnvironment("IDENTITY_HTTP", identity.GetEndpoint("http"))
  .WaitFor(server)
  .PublishAsDockerComposeService((_, service) => { service.Name = "webfrontend"; });

var commandService = builder.AddProject<Projects.EMMA_CommandService>("command-service")
  .WithReference(kafka)
  .WaitFor(kafka)
  .PublishAsDockerComposeService((_, service) => { service.Name = "command-service"; });

var api = builder.AddProject<Projects.EMMA_Api>("emma-api")
  .WithReference(appDb)
  .WithReference(telemetryDb)
  .WithReference(identityDb)
  .WithReference(kafka) // if needed later
  .WithEnvironment("Jwt__Key", jwtKey)
  .WithEnvironment("Jwt__Issuer", "emma-identity")
  .WithEnvironment("Jwt__Audience", "emma-api")
  .WaitFor(appDb)
  .WaitFor(telemetryDb)
  .PublishAsDockerComposeService((_, service) => { service.Name = "emma-api"; });

var solarForecaster = builder.AddPythonApp("solar-forecaster", "../EMMA.SolarForecaster", "main.py")
  .WithReference(telemetryDb)
  .WithReference(kafka)
  .WaitFor(telemetryDb)
  .PublishAsDockerComposeService((_, service) => { service.Name = "solar-forecaster"; });

var optimizer = builder.AddPythonApp("optimizer", "../EMMA.Optimizer", "main.py")
  .WithReference(telemetryDb)
  .WithReference(kafka)
  .WaitFor(telemetryDb)
  .WaitFor(solarForecaster)
  .PublishAsDockerComposeService((_, service) => { service.Name = "optimizer"; });

builder.Build().Run();
