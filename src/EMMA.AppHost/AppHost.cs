var builder = DistributedApplication.CreateBuilder(args);

var postgresql = builder.AddPostgres("postgresql")
    .WithImage("timescale/timescaledb", "latest-pg17")
    .WithPgAdmin();

var emma_db = postgresql.AddDatabase("emma-db");

var kafka = builder.AddKafka("messaging")
    .WithKafkaUI();

var jwtKey = builder.AddParameter("jwt-key", secret: true);
var entsoeApiKey = builder.AddParameter("entsoe-api-key", secret: true);

var mqttBridge = builder.AddDockerfile("mqtt-bridge", "../simple-mqtt-kafka-bridge")
                        .WithReference(kafka)
                        .WaitFor(kafka)
                        .WithEnvironment("KAFKA_BROKERS", $"{kafka.Resource.Name}:9093")
                        .WithEnvironment("KAFKA_TOPIC", "telemetry-raw")
                        .WithEndpoint(targetPort: 1883, name: "mqtt-port");

var simulator = builder.AddPythonApp("energy-simulator", "../energy-simulator", "main.py")
    .WithEnvironment("MQTT_BROKER_URL", mqttBridge.GetEndpoint("mqtt-port"))
    .WaitFor(mqttBridge);

var server = builder.AddProject<Projects.EMMA_Server>("server")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithReference(emma_db)
    .WaitFor(emma_db);

var marketService = builder.AddProject<Projects.EMMA_MarketService>("market-service")
    .WithReference(kafka)
    .WithReference(emma_db)
    .WithEnvironment("Entsoe__ApiKey", entsoeApiKey)
    .WaitFor(kafka)
    .WaitFor(server);

var ingestion = builder.AddProject<Projects.EMMA_Ingestion>("ingestion")
    .WithReference(emma_db)
    .WithReference(kafka)
    .WaitFor(emma_db)
    .WaitFor(kafka)
    .WaitFor(server);

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(server)
    .WaitFor(server);

var commandService = builder.AddProject<Projects.EMMA_CommandService>("command-service")
    .WithReference(kafka)
    .WaitFor(kafka);

var api = builder.AddProject<Projects.EMMA_Api>("emma-api")
    .WithReference(emma_db)
    .WithReference(kafka) // if needed later
    .WithEnvironment("Jwt__Key", jwtKey)
    .WaitFor(emma_db);

var identity = builder.AddProject<Projects.Emma_Identity>("emma-identity")
    .WithReference(emma_db)
    .WithEnvironment("Jwt__Key", jwtKey)
    .WaitFor(emma_db);

builder.Build().Run();
