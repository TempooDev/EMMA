var builder = DistributedApplication.CreateBuilder(args);

var postgresql = builder.AddPostgres("postgresql")
    .WithImage("timescale/timescaledb", "latest-pg17");

var emma_db = postgresql.AddDatabase("emma-db");

var kafka = builder.AddKafka("messaging")
    .WithImage("confluentinc/cp-kafka", "latest")
    .WithKafkaUI();

var mqttBridge = builder.AddContainer("mqtt-bridge", "simple-mqtt-kafka-bridge")
                        .WithReference(kafka)
                        .WithEnvironment("KAFKA_TOPIC", "telemetry-raw")
                        .WithHttpEndpoint(port: 1883, name: "mqtt-port");

var ingest = builder.AddGolangApp("ingest", "../ingest")
    .WithReference(emma_db);

var server = builder.AddProject<Projects.EMMA_Server>("server")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithReference(emma_db);

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(server)
    .WaitFor(server);

server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
