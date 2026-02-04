var builder = DistributedApplication.CreateBuilder(args);

var postgresql = builder.AddPostgres("postgresql")
    .WithImage("timescale/timescaledb", "latest-pg17")
    .WithPgAdmin();

var identityDb = postgresql.AddDatabase("identity-db");
var appDb = postgresql.AddDatabase("app-db");
var telemetryDb = postgresql.AddDatabase("telemetry-db");

var kafka = builder.AddKafka("messaging")
    .WithKafkaUI();

var jwtKey = builder.AddParameter("jwt-key", secret: true);

var mqttBridge = builder.AddDockerfile("mqtt-bridge", "../simple-mqtt-kafka-bridge")
                        .WithReference(kafka)
                        .WaitFor(kafka)
                        .WithEnvironment("KAFKA_BROKERS", $"{kafka.Resource.Name}:9093")
                        .WithEnvironment("KAFKA_TOPIC", "telemetry-raw")
                        .WithEndpoint(targetPort: 1883, name: "mqtt-port");

var simulator = builder.AddPythonApp("energy-simulator", "../energy-simulator", "main.py")
    .WithEnvironment("MQTT_BROKER_URL", mqttBridge.GetEndpoint("mqtt-port"))
    .WithEnvironment("TENANT_ID", "T001")
    .WaitFor(mqttBridge);

var server = builder.AddProject<Projects.EMMA_Server>("server")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithReference(appDb)
    .WithReference(telemetryDb)
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithEnvironment("Jwt__Issuer", "emma-identity")
    .WithEnvironment("Jwt__Audience", "emma-api")
    .WaitFor(appDb)
    .WaitFor(telemetryDb);

var marketService = builder.AddProject<Projects.EMMA_MarketService>("market-service")
    .WithReference(kafka)
    .WithReference(telemetryDb)
    .WaitFor(kafka)
    .WaitFor(server);

var ingestion = builder.AddProject<Projects.EMMA_Ingestion>("ingestion")
    .WithReference(appDb)
    .WithReference(telemetryDb)
    .WithReference(kafka)
    .WaitFor(appDb)
    .WaitFor(telemetryDb)
    .WaitFor(kafka)
    .WaitFor(server);

var identity = builder.AddProject<Projects.Emma_Identity>("emma-identity")
    .WithReference(identityDb)
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithEnvironment("Jwt__Issuer", "emma-identity")
    .WithEnvironment("Jwt__Audience", "emma-api")
    .WaitFor(identityDb);

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(server)
    .WithReference(identity)
    .WithEnvironment("SERVER_HTTP", server.GetEndpoint("http"))
    .WithEnvironment("IDENTITY_HTTP", identity.GetEndpoint("http"))
    .WaitFor(server);

var commandService = builder.AddProject<Projects.EMMA_CommandService>("command-service")
    .WithReference(kafka)
    .WaitFor(kafka);

var api = builder.AddProject<Projects.EMMA_Api>("emma-api")
    .WithReference(appDb)
    .WithReference(telemetryDb)
    .WithReference(identityDb)
    .WithReference(kafka) // if needed later
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithEnvironment("Jwt__Issuer", "emma-identity")
    .WithEnvironment("Jwt__Audience", "emma-api")
    .WaitFor(appDb)
    .WaitFor(telemetryDb);

var solarForecaster = builder.AddPythonApp("solar-forecaster", "../EMMA.SolarForecaster", "main.py")
    .WithReference(telemetryDb)
    .WithReference(kafka)
    .WaitFor(telemetryDb);

var optimizer = builder.AddPythonApp("optimizer", "../EMMA.Optimizer", "main.py")
    .WithReference(telemetryDb)
    .WithReference(kafka)
    .WaitFor(telemetryDb)
    .WaitFor(solarForecaster);

builder.Build().Run();
