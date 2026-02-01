package database

import (
	"context"
	"fmt"

	"github.com/jackc/pgx/v5"
)

func NewPostgresConnection(ctx context.Context, connectionString string) (*pgx.Conn, error) {
	conn, err := pgx.Connect(ctx, connectionString)
	if err != nil {
		return nil, fmt.Errorf("unable to connect to database: %w", err)
	}
	return conn, nil
}
