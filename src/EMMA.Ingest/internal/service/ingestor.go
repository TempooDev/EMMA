package service

import (
	"context"
	"encoding/json"
	"log"

	"github.com/jackc/pgx/v5"
	"github.com/segmentio/kafka-go"

	"emma/ingestor/internal/config"
	"emma/ingestor/internal/model"
)

type Ingestor struct {
	cfg    *config.Config
	db     *pgx.Conn
	reader *kafka.Reader
}

func NewIngestor(cfg *config.Config, db *pgx.Conn, reader *kafka.Reader) *Ingestor {
	return &Ingestor{
		cfg:    cfg,
		db:     db,
		reader: reader,
	}
}

func (s *Ingestor) Start(ctx context.Context) error {
	defer s.reader.Close()
	log.Printf("EMMA Ingestor started. Listening on topic: %s", s.cfg.Topic)

	for {
		// ReadMessage automatically handles context cancellation
		msg, err := s.reader.ReadMessage(ctx)
		if err != nil {
			if ctx.Err() != nil {
				return ctx.Err()
			}
			log.Printf("Consumer error: %v", err)
			continue
		}

		var data model.Telemetry
		if err := json.Unmarshal(msg.Value, &data); err != nil {
			log.Printf("Error decoding JSON: %v", err)
			continue
		}

		s.processTelemetry(data)
	}
}

func (s *Ingestor) processTelemetry(data model.Telemetry) {
	// Logic to persist in Hypertable
	// Senior Tip: Use transactions if inserting multiple metrics per message
	log.Printf("Processing Asset: %s - MsgID: %s", data.Metadata.AssetID, data.Metadata.MessageID)
	// actual DB insertion implementation would go here using s.db
}
