package config

import (
	"fmt"
	"os"
)

type Config struct {
	BootstrapServers   string
	GroupID            string
	AutoOffsetReset    string
	Topic              string
	DBConnectionString string
}

func Load() (*Config, error) {
	bootstrapServers := os.Getenv("ConnectionStrings__messaging")
	if bootstrapServers == "" {
		return nil, fmt.Errorf("ConnectionStrings__messaging environment variable is required")
	}

	dbConnString := os.Getenv("EMMA_DB_URI")
	if dbConnString == "" {
		return nil, fmt.Errorf("EMMA_DB_URI environment variable is required")
	}

	return &Config{
		BootstrapServers:   bootstrapServers,
		GroupID:            "emma-ingestor-group",
		AutoOffsetReset:    "earliest",
		Topic:              "telemetry-raw",
		DBConnectionString: dbConnString,
	}, nil
}
