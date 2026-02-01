package messaging

import (
	"fmt"

	"github.com/confluentinc/confluent-kafka-go/v2/kafka"
)

func NewKafkaConsumer(bootstrapServers, groupID, autoOffsetReset string) (*kafka.Consumer, error) {
	c, err := kafka.NewConsumer(&kafka.ConfigMap{
		"bootstrap.servers": bootstrapServers,
		"group.id":          groupID,
		"auto.offset.reset": autoOffsetReset,
	})

	if err != nil {
		return nil, fmt.Errorf("failed to create kafka consumer: %w", err)
	}

	return c, nil
}
