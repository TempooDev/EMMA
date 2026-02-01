package messaging

import (
	"github.com/segmentio/kafka-go"
)

// NewKafkaConsumer creates a new Kafka Reader (consumer group)
func NewKafkaConsumer(brokers []string, groupID, topic string) *kafka.Reader {
	return kafka.NewReader(kafka.ReaderConfig{
		Brokers:  brokers,
		GroupID:  groupID,
		Topic:    topic,
		MinBytes: 10e3, // 10KB
		MaxBytes: 10e6, // 10MB
	})
}
