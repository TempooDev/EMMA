package service

import (
	"context"
	"encoding/json"
	"log"
	"time"

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

func (s *Ingestor) StartPriceFetcher(ctx context.Context) {
	log.Println("Starting Price Fetcher service...")
	ticker := time.NewTicker(1 * time.Hour)
	defer ticker.Stop()

	// Initial run
	go s.fetchAndSavePrices(ctx)

	for {
		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
			go s.fetchAndSavePrices(ctx)
		}
	}
}

func (s *Ingestor) fetchAndSavePrices(ctx context.Context) {
	log.Println("Fetching market prices...")
	prices := simulateVerifyExternalAPI()

	for _, p := range prices {
		t := p.Time.UTC()
		if p.Price < 0 {
			log.Printf("ALERT: Negative price detected! %v at %v (%s)\n", p.Price, t, p.Source)
		}

		query := `
			INSERT INTO market_prices (time, price, currency, source)
			VALUES ($1, $2, $3, $4)
			ON CONFLICT (time, source) DO UPDATE 
			SET price = EXCLUDED.price;
		`
		_, err := s.db.Exec(ctx, query, t, p.Price, p.Currency, p.Source)
		if err != nil {
			log.Printf("Error inserting price: %v\n", err)
		}
	}
	log.Println("Market prices saved successfully.")
}

type MarketPrice struct {
	Time     time.Time
	Price    float64
	Currency string
	Source   string
}

func simulateVerifyExternalAPI() []MarketPrice {
	now := time.Now().Truncate(time.Hour)
	var prices []MarketPrice
	// Generate 24 hours of data
	for i := 0; i < 24; i++ {
		t := now.Add(time.Duration(i) * time.Hour)
		// Random price between -10 and 100
		// Note: rand.Float64() uses global rand, seeded by default with 1 (deterministic).
		// For simulation it's fine.
		val := (float64(time.Now().UnixNano()%110) - 10) // simple pseudo-random based on time
		
		prices = append(prices, MarketPrice{
			Time:     t,
			Price:    val,
			Currency: "EUR",
			Source:   "SIMULATED_ENTSO_E",
		})
	}
	return prices
}
