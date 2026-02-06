#!/bin/sh
set -e

# Función para crear bases de datos de forma segura
# Usamos 'psql' para ejecutar el comando fuera de bloques transaccionales
for db in "identity-db" "app-db" "telemetry-db"; do
  echo "Revisando base de datos: $db"
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    SELECT 'CREATE DATABASE "$db"'
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$db')\gexec
EOSQL
done