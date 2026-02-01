package main

import (
	"context"
	"log"

	"emma/ingestor/internal/config"
	"emma/ingestor/internal/infrastructure/database"
	"emma/ingestor/internal/infrastructure/messaging"
	"emma/ingestor/internal/service"
)

func main() {
	// 1. Load Configuration
	cfg, err := config.Load()
	if err != nil {
		log.Fatalf("Failed to load configuration: %v", err)
	}

	// 2. Connect to TimescaleDB
	ctx := context.Background()
	conn, err := database.NewPostgresConnection(ctx, cfg.DBConnectionString)
	if err != nil {
		log.Fatalf("Error connecting to DB: %v", err)
	}
	defer conn.Close(ctx)

	// 3. Setup Kafka Reader
	// Note: We're passing BootstrapServers as a slice. If it's a comma-separated list, 
	// we might need strings.Split(cfg.BootstrapServers, ","), but wrapping in []string{}
	// is safe for a single broker.
	reader := messaging.NewKafkaConsumer([]string{cfg.BootstrapServers}, cfg.GroupID, cfg.Topic)
	defer reader.Close()

	// 4. Start Ingestor Service
	ingestor := service.NewIngestor(cfg, conn, reader)
	if err := ingestor.Start(ctx); err != nil {
		log.Fatalf("Ingestor service failed: %v", err)
	}
}
