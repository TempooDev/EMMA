package model

// Telemetry represents the telemetry data structure
type Telemetry struct {
	Metadata struct {
		MessageID string `json:"message_id"`
		AssetID   string `json:"asset_id"`
	} `json:"metadata"`
	Payload struct {
		Timestamp string `json:"timestamp"`
		Metrics   []struct {
			Name  string  `json:"name"`
			Value float64 `json:"value"`
			Unit  string  `json:"unit"`
		} `json:"metrics"`
	} `json:"payload"`
}
