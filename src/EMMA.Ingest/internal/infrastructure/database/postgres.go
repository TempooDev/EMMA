package database

import (
	"context"
	"fmt"

	"strings"

	"github.com/jackc/pgx/v5"
)

func NewPostgresConnection(ctx context.Context, connectionString string) (*pgx.Conn, error) {
	normalizedConnString := convertNpgsqlToLibpq(connectionString)
	conn, err := pgx.Connect(ctx, normalizedConnString)
	if err != nil {
		return nil, fmt.Errorf("unable to connect to database: %w", err)
	}
	return conn, nil
}

func convertNpgsqlToLibpq(connStr string) string {
	replacements := map[string]string{
		"User ID=":  "user=",
		"Username=": "user=",
		"UserID=":   "user=",
		"Server=":   "host=",
		"Database=": "dbname=",
		"Port=":     "port=",
		"Password=": "password=",
	}

	for k, v := range replacements {
		connStr = strings.ReplaceAll(connStr, k, v)
	}
	return connStr
}
