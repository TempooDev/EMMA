package service

import (
	"context"
	"encoding/json"
	"log"

	"github.com/confluentinc/confluent-kafka-go/v2/kafka"
	"github.com/jackc/pgx/v5"

	"emma/ingestor/internal/config"
	"emma/ingestor/internal/model"
)

type Ingestor struct {
	cfg      *config.Config
	db       *pgx.Conn
	consumer *kafka.Consumer
}

func NewIngestor(cfg *config.Config, db *pgx.Conn, consumer *kafka.Consumer) *Ingestor {
	return &Ingestor{
		cfg:      cfg,
		db:       db,
		consumer: consumer,
	}
}

func (s *Ingestor) Start(ctx context.Context) error {
	err := s.consumer.SubscribeTopics([]string{s.cfg.Topic}, nil)
	if err != nil {
		return err
	}
	log.Printf("EMMA Ingestor started. Listening on topic: %s", s.cfg.Topic)

	for {
		select {
		case <-ctx.Done():
			return ctx.Err()
		default:
			msg, err := s.consumer.ReadMessage(-1)
			if err == nil {
				var data model.Telemetry
				if err := json.Unmarshal(msg.Value, &data); err != nil {
					log.Printf("Error decoding JSON: %v", err)
					continue
				}

				s.processTelemetry(data)
			} else {
				// Kafka errors can be transient, log and continue
				log.Printf("Consumer error: %v", err)
			}
		}
	}
}

func (s *Ingestor) processTelemetry(data model.Telemetry) {
	// Logic to persist in Hypertable
	// Senior Tip: Use transactions if inserting multiple metrics per message
	log.Printf("Processing Asset: %s - MsgID: %s", data.Metadata.AssetID, data.Metadata.MessageID)
	// actual DB insertion implementation would go here using s.db
}
