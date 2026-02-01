var builder = DistributedApplication.CreateBuilder(args);

var postgresql = builder.AddPostgres("postgresql")
    .WithImage("timescale/timescaledb", "latest-pg17")
    .WithPgAdmin();

var emma_db = postgresql.AddDatabase("emma-db");

var kafka = builder.AddKafka("messaging")
    .WithKafkaUI();

var mqttBridge = builder.AddDockerfile("mqtt-bridge", "../simple-mqtt-kafka-bridge")
                        .WithReference(kafka)
                        .WaitFor(kafka)
                        .WithEnvironment("KAFKA_BROKERS", $"{kafka.Resource.Name}:9093")
                        .WithEnvironment("KAFKA_TOPIC", "telemetry-raw")
                        .WithEndpoint(targetPort: 1883, name: "mqtt-port");

var ingest = builder.AddGolangApp("ingest", "../EMMA.Ingest")
    .WithReference(emma_db)
    .WaitFor(emma_db)
    .WithReference(kafka)
    .WaitFor(kafka);

var server = builder.AddProject<Projects.EMMA_Server>("server")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithReference(emma_db)
    .WaitFor(emma_db);

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(server)
    .WaitFor(server);

builder.Build().Run();
