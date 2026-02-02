package service

import (
	"context"
	"encoding/json"
	"log"
	"time"

	"github.com/jackc/pgx/v5"
	"github.com/segmentio/kafka-go"

	"emma/ingestor/internal/config"
	"emma/ingestor/internal/infrastructure/reddata"
	"net"
	"strconv"

	"emma/ingestor/internal/model"
)

type Ingestor struct {
	cfg    *config.Config
	db     *pgx.Conn
	reader *kafka.Reader
	writer *kafka.Writer
}

func NewIngestor(cfg *config.Config, db *pgx.Conn, reader *kafka.Reader) *Ingestor {
	// Simple Kafka Writer for alerts
	// Assuming cfg.BootstrapServers is available/parsable.
	// For simplicity, hardcoding topic 'price-alert' or could add to config.
	writer := &kafka.Writer{
		Addr:     kafka.TCP(cfg.BootstrapServers),
		Topic:    "price-alert",
		Balancer: &kafka.LeastBytes{},
	}

	return &Ingestor{
		cfg:    cfg,
		db:     db,
		reader: reader,
		writer: writer,
	}
}

func (s *Ingestor) Start(ctx context.Context) error {
	defer s.reader.Close()
	
	// Create price-alert topic explicitly to avoid reliance on auto.create.topics.enable
	s.ensureTopic(ctx, "price-alert")

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
	log.Println("Starting Price Fetcher service (Cron: @hourly)...")
	
	// 1. Run immediately on startup (optional, but good to populate data ASAP)
	go s.fetchAndSavePrices(ctx)

	// 2. Calculate time until next hour for alignment
	now := time.Now()
	nextHour := now.Truncate(time.Hour).Add(1 * time.Hour)
	delay := nextHour.Sub(now)

	log.Printf("Next scheduled run in %v (at %v)", delay, nextHour.Format(time.TimeOnly))

	// Timer for the first aligned run
	timer := time.NewTimer(delay)
	
	go func() {
		<-timer.C
		// First aligned run
		s.fetchAndSavePrices(ctx)
		
		// Then ticker every hour
		ticker := time.NewTicker(1 * time.Hour)
		defer ticker.Stop()
		
		for {
			select {
			case <-ctx.Done():
				return
			case <-ticker.C:
				go s.fetchAndSavePrices(ctx)
			}
		}
	}()
}

func (s *Ingestor) fetchAndSavePrices(ctx context.Context) {
	// Ensure schema exists before we try to insert anything
	if err := s.ensureSchema(ctx); err != nil {
		log.Printf("Error ensuring schema: %v", err)
		// Proceeding might fail, but let's try
	}

	log.Println("Fetching market prices from REDData...")
	
	client := reddata.NewClient()
	// Truncate to hour to ensure cleaner API requests (avoiding minutes/seconds issues)
	now := time.Now().Truncate(time.Hour)
	start := now.Add(-24 * time.Hour) // Last 24 hours
	end := now

	// 1. PVPC (Prices)
	resp, err := client.FetchData("mercados", "precios-mercados-tiempo-real", start, end, "hour")
	if err != nil {
		log.Printf("Error fetching prices: %v", err)
		return
	}

	for _, included := range resp.Included {
		log.Printf("Processing %s", included.Attributes.Title)
		for _, v := range included.Attributes.Values {
			// Parse Time
			t, err := time.Parse("2006-01-02T15:04:05.000-07:00", v.Datetime)
			if err != nil {
				// Try another format just in case
				t, err = time.Parse("2006-01-02T15:04:05", v.Datetime)
				if err != nil {
					log.Printf("Error parsing date %s: %v", v.Datetime, err)
					continue 
				}
			}
			
			// Log as requested
			log.Printf("[%s] Time: %s, Value: %.2f", included.Attributes.Title, t.Format(time.DateTime), v.Value)

			// Save to DB
			query := `
				INSERT INTO raw_data.market_prices (time, price, currency, unit, source)
				VALUES ($1, $2, $3, $4, $5)
				ON CONFLICT (time, source) DO UPDATE 
				SET price = EXCLUDED.price;
			`
			_, err = s.db.Exec(ctx, query, t, v.Value, "EUR", "€/MWh", "REE_PVPC")
			if err != nil {
				log.Printf("Error inserting price: %v\n", err)
			}

			// Check Alert
			if v.Value < 0 {
				if err := s.publishPriceAlert(ctx, t, v.Value); err != nil {
					log.Printf("Error publishing alert: %v", err)
				}
			}
		}
	}
	
	// Fetch and Log other data types as requested
	// Fetch and Save other data types as requested
	s.fetchAndSaveEnergyData(ctx, client, start, end)
	
	log.Println("Market prices and energy data saved.")
}

func (s *Ingestor) ensureSchema(ctx context.Context) error {
	queries := []string{
		// 1. Create Schema
		"CREATE SCHEMA IF NOT EXISTS raw_data;",
		
		// 2. Tables within raw_data schema
		`CREATE TABLE IF NOT EXISTS raw_data.market_prices (
			time TIMESTAMPTZ NOT NULL,
			price DOUBLE PRECISION NOT NULL,
			currency TEXT NOT NULL,
			unit TEXT DEFAULT '€/MWh',
			source TEXT NOT NULL,
			PRIMARY KEY (time, source)
		);`,
		`CREATE TABLE IF NOT EXISTS raw_data.energy_metrics (
			time TIMESTAMPTZ NOT NULL,
			metric_name TEXT NOT NULL,
			value DOUBLE PRECISION NOT NULL,
			unit TEXT,
			source TEXT NOT NULL,
			PRIMARY KEY (time, metric_name, source)
		);`,
		// Ensure TimescaleDB hypertable
		"SELECT create_hypertable('raw_data.market_prices', 'time', if_not_exists => TRUE, migrate_data => TRUE);", 
		"SELECT create_hypertable('raw_data.energy_metrics', 'time', if_not_exists => TRUE, migrate_data => TRUE);",
	}

	for _, q := range queries {
		if _, err := s.db.Exec(ctx, q); err != nil {
			// Start price fetcher even if hypertable creation fails (e.g. extension not installed)
			log.Printf("Warning executing DB query '%s': %v", q, err)
		}
	}
	return nil
}

func (s *Ingestor) fetchAndSaveEnergyData(ctx context.Context, client *reddata.Client, start, end time.Time) {
	// 2. Generation Mix
	s.fetchAndSaveGeneric(ctx, client, "generacion", "estructura-generacion", start, end)
	
	// 3. Demand
	s.fetchAndSaveGeneric(ctx, client, "demanda", "evolucion", start, end)
	
	// 4. CO2 Emissions
	s.fetchAndSaveGeneric(ctx, client, "generacion", "no-renovables-detalle-emisiones", start, end)
}

func (s *Ingestor) fetchAndSaveGeneric(ctx context.Context, client *reddata.Client, category, widget string, start, end time.Time) {
	resp, err := client.FetchData(category, widget, start, end, "hour")
	if err != nil {
		log.Printf("Error fetching %s/%s: %v", category, widget, err)
		return
	}
	
	for _, included := range resp.Included {
		// Group by Name (Title)
		// included.Attributes.Title is the metric name (e.g. "Solar fotovoltaica", "Demanda real")
		for _, v := range included.Attributes.Values {
			t, err := time.Parse("2006-01-02T15:04:05.000-07:00", v.Datetime)
			if err != nil {
				t, err = time.Parse("2006-01-02T15:04:05", v.Datetime)
				if err != nil {
					log.Printf("Error parsing date %s: %v", v.Datetime, err)
					continue
				}
			}

			// Save to DB
			query := `
				INSERT INTO raw_data.energy_metrics (time, metric_name, value, unit, source)
				VALUES ($1, $2, $3, $4, $5)
				ON CONFLICT (time, metric_name, source) DO UPDATE 
				SET value = EXCLUDED.value;
			`
			// Unit is unknown for now, leaving empty or could try to infer
			_, err = s.db.Exec(ctx, query, t, included.Attributes.Title, v.Value, "", "REE_API")
			if err != nil {
				log.Printf("Error inserting metric %s: %v\n", included.Attributes.Title, err)
			}
		}
	}
	log.Printf("Saved data for %s/%s", category, widget)
}

func (s *Ingestor) publishPriceAlert(ctx context.Context, t time.Time, price float64) error {
	log.Printf("[ALERT] Negative price detected: %.2f at %v. Sending to Kafka...", price, t)
	
	alertPayload := map[string]interface{}{
		"event": "price_alert",
		"type": "negative_price",
		"timestamp": t,
		"price": price,
		"currency": "EUR",
		"unit": "€/MWh",
	}
	
	val, err := json.Marshal(alertPayload)
	if err != nil {
		return err
	}

	return s.writer.WriteMessages(ctx, kafka.Message{
		Key:   []byte("price-alert"),
		Value: val,
		Time:  time.Now(),
	})
}

func (s *Ingestor) ensureTopic(ctx context.Context, topic string) {
	// Quick robust implementation to create topic if not exists
	conn, err := kafka.Dial("tcp", s.cfg.BootstrapServers)
	if err != nil {
		log.Printf("Warning: failed to dial kafka to ensure topic: %v", err)
		return
	}
	defer conn.Close()

	controller, err := conn.Controller()
	if err != nil {
		log.Printf("Warning: failed to get kafka controller: %v", err)
		return
	}

	controllerConn, err := kafka.Dial("tcp", net.JoinHostPort(controller.Host, strconv.Itoa(controller.Port)))
	if err != nil {
		log.Printf("Warning: failed to dial kafka controller: %v", err)
		return
	}
	defer controllerConn.Close()

	topicConfigs := []kafka.TopicConfig{
		{
			Topic:             topic,
			NumPartitions:     1,
			ReplicationFactor: 1,
		},
	}

	err = controllerConn.CreateTopics(topicConfigs...)
	if err != nil {
		// Ignore if topic already exists
		log.Printf("ensureTopic info: %v", err)
	} else {
		log.Printf("Topic '%s' ensured (created or existed)", topic)
	}
}


type MarketPrice struct {
	Time     time.Time
	Price    float64
	Currency string
	Source   string
}

// simulateVerifyExternalAPI removed

