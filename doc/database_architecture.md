# Database Architecture

EMMA uses a **multi-database architecture** to separate concerns and optimize for different workload patterns:

## Database Overview

| Database | Purpose | Tables | Access Pattern |
|----------|---------|--------|----------------|
| **identity-db** | Authentication & Authorization | `users`, `api_keys`, `audit_logs` | Low volume, high security |
| **app-db** | Business Logic & Metadata | `devices`, `assets`, `energy_communities`, `asset_mappings`, `processed_messages` | Medium volume, transactional |
| **telemetry-db** | Time-Series Metrics | `asset_metrics`, `market_prices`, `interconnection_flows`, `optimization_schedules` | High volume, append-heavy |

## Architecture Rationale

### Why Separate Databases?

1. **Performance Isolation**: Time-series telemetry data doesn't impact authentication or business logic queries.
2. **Scalability**: Each database can be scaled independently based on its workload.
3. **Security**: Sensitive identity data is isolated from operational data.
4. **Backup Strategy**: Different retention policies for each database type.

### Technology Choices

- **identity-db**: Standard PostgreSQL (ACID compliance for auth)
- **app-db**: Standard PostgreSQL (referential integrity for business logic)
- **telemetry-db**: TimescaleDB (optimized for time-series data with compression)

## Service Database Access

| Service | Databases | Purpose |
|---------|-----------|---------|
| **Emma.Identity** | `identity-db` | User authentication, JWT token generation |
| **EMMA.Server** | `app-db`, `telemetry-db` | Dashboard queries, cross-database aggregations |
| **EMMA.Api** | `app-db`, `telemetry-db`, `identity-db` | External API with full data access |
| **EMMA.Ingestion** | `app-db`, `telemetry-db` | Asset mapping (app) + metric storage (telemetry) |
| **EMMA.MarketService** | `telemetry-db` | Market price and flow data storage |
| **SolarForecaster** | `telemetry-db` | Historical metrics for ML training |
| **Optimizer** | `telemetry-db` | Price forecasts and optimization schedules |

## Cross-Database Queries

Since PostgreSQL doesn't support efficient cross-database JOINs, EMMA uses **application-level joins**:

### Example: Device Status Query

```csharp
// 1. Fetch device IDs from app-db
var deviceIds = await appDb.QueryAsync<string>(
    "SELECT device_id FROM devices WHERE tenant_id = @TenantId", 
    new { TenantId });

// 2. Fetch metrics from telemetry-db using device IDs
var metrics = await telemetryDb.QueryAsync(
    "SELECT * FROM asset_metrics WHERE asset_id = ANY(@DeviceIds)", 
    new { DeviceIds = deviceIds.ToArray() });
```

### Foreign Key Constraints

Cross-database foreign keys are **not enforced at the database level**. Instead:

- **Validation**: Application code validates relationships before writes.
- **Referential Integrity**: Handled in service layer logic.
- **Example**: `assets.user_id` references `users.id` (in identity-db), but the FK constraint is removed.

## Schema Initialization

The `DbInitializer` service in `EMMA.Server` initializes all three databases on startup:

```csharp
await InitializeDatabaseAsync(appDataSource, SchemaSql.AppScripts, "AppDB");
await InitializeDatabaseAsync(telemetryDataSource, SchemaSql.TelemetryScripts, "TelemetryDB");
```

Identity database is initialized by `Emma.Identity` service independently.

## Connection Management

Services use **keyed datasources** for dependency injection:

```csharp
// Registration
builder.Services.AddKeyedSingleton<NpgsqlDataSource>("app-db", ...);
builder.Services.AddKeyedSingleton<NpgsqlDataSource>("telemetry-db", ...);

// Consumption
public class MyRepository(
    [FromKeyedServices("app-db")] NpgsqlDataSource appDb,
    [FromKeyedServices("telemetry-db")] NpgsqlDataSource telemetryDb)
{
    // Use appropriate datasource for each query
}
```

## Migration from Monolithic Database

The original `emma-db` was split into three databases:

1. **Identity tables** → `identity-db`
2. **Business logic tables** → `app-db`
3. **Time-series tables** → `telemetry-db`

### Breaking Changes

- Cross-database foreign keys removed (enforced at application level)
- Services requiring multiple databases now inject multiple datasources
- Queries spanning databases refactored to use application-level joins

## Performance Considerations

### TimescaleDB Optimizations

The `telemetry-db` uses TimescaleDB features:

- **Hypertables**: Automatic partitioning by time for `asset_metrics`, `market_prices`
- **Compression**: Automatic compression after 30 days
- **Continuous Aggregates**: Pre-computed hourly/daily rollups
- **Retention Policies**: Automatic data cleanup after 90 days

### Connection Pooling

Each datasource maintains its own connection pool, configured via Aspire:

```csharp
var appDb = postgresql.AddDatabase("app-db");
var telemetryDb = postgresql.AddDatabase("telemetry-db");
```

## Monitoring

Use the Aspire Dashboard to monitor:

- **Database Health**: Check all three databases are running
- **Connection Counts**: Monitor pool usage per database
- **Query Performance**: Identify slow queries in each database

## Future Enhancements

- **Read Replicas**: Add read replicas for `telemetry-db` to scale query load
- **Sharding**: Partition `telemetry-db` by market zone for geographic distribution
- **Caching**: Add Redis layer for frequently accessed app-db queries
