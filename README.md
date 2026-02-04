### Distributed Energy Management System (DEMS) & Real-Time Market Intelligence

**EMMA** is a distributed software platform designed for monitoring, analyzing, and optimizing energy assets in the European market. The platform solves the challenge of **price decoupling** and **renewable energy intermittency**, allowing energy communities and industrial operators to make decisions based on real-time data.

## 🚀 Project Purpose

In a market where prices can range from €130/MWh in France to negative values in Spain, EMMA acts as a centralized brain that:

- **Synchronizes:** Ingests massive telemetry from IoT assets (solar panels, batteries, EV chargers) using lightweight protocols.
- **Analyzes:** Processes data from the Spanish market operator (**REData**) and European markets to identify opportunities for **energy arbitrage**.
- **Optimizes:** Automates asset consumption when the grid has surpluses (negative prices), reducing the carbon footprint and operating costs.

## 🛠️ Technology Stack & Architecture

Designed with a focus on **decoupled microservices** and high availability:

- **Ingestion & Strategy Engine:** Distributed microservices in .NET and Python processing messages via **MQTT/Kafka**.
- **Data Core:** Multi-database architecture with three specialized PostgreSQL instances:
  - **IdentityDB**: Authentication, API keys, and audit logs
  - **AppDB**: Business logic, devices, assets, and communities
  - **TelemetryDB**: TimescaleDB for massive time-series storage with native compression
  - See [Database Architecture](doc/database_architecture.md) for details

- **Market Intelligence:** Integration with the **REData** API for monitoring intraday prices and European interconnection congestion.
- **Frontend Observability:** Real-time visualization and asset monitoring using **React**, **Recharts**, and **Leaflet** for interactive geographic distribution.
- **Infrastructure:** Orchestrated deployment using **Docker** and .NET Aspire for microservice management.

## 📦 Services & Components

| Service | Goal | Technology |
| :--- | :--- | :--- |
| **EMMA.AppHost** | Main orchestrator managing service lifecycle and network dependencies. | .NET Aspire |
| **Emma.Server** | Core API providing dashboard metrics, asset control, and arbitrage analytics. | .NET 9 |
| **Emma.Dashboard** | High-performance React frontend providing real-time observability, predictive analytics, and asset maps. | React + Vite |
| **EMMA.MarketService** | Continuous monitor for market prices (**REData**), congestion, and inter-border flows. | .NET Worker |
| **EMMA.CommandService** | Asset controller executing real-time setpoint adjustments and arbitrage strategies. | .NET Worker |
| **EMMA.SolarForecaster**| Machine learning service providing solar production forecasts for the next 24h. | Python (FastAPI) |
| **EMMA.Optimizer** | Advanced optimization engine calculating the best setpoints for multi-asset communities. | Python |
| **Energy Simulator** | Multi-asset IoT simulator generating realistic telemetry and price scenarios. | Python |
| **EMMA.Ingestion** | Scalable telemetry ingestor processing MQTT/Kafka streams into TimescaleDB. | .NET Worker |
| **MQTT-Kafka Bridge** | High-performance message bridge for translating IoT protocols to the data bus. | Fluent Bit |
| **TimescaleDB Core** | Time-series database optimized for energy metrics with native compression. | TimescaleDB |
| **Emma.Identity** | Robust authentication service ensuring multi-tenant data isolation. | .NET 9 |

## 🇪🇺 Compliance & EU Standards

- **GDPR Ready:** Implementation of consumption data anonymization at the database level.
- **Multi-Region:** Native support for multiple time zones (CET/EET/WET) and intra-community VAT management.
- **Energy Efficiency:** Optimized algorithms for handling negative prices resulting from renewable overproduction.

## 🧪 How to Test & Verify Features

### 1. Running the Platform
Ensure you have the .NET SDK and Docker installed, then run:
```bash
dotnet run --project src/EMMA.AppHost
```
Open the **Aspire Dashboard** (link provided in terminal) to monitor all microservices or use the **Emma Dashboard** for asset observability.

### 2. Audit Logging
To verify that sensitive actions are tracked:
1. Use the **Swagger UI** in `Emma.Api` to perform a `POST` or `PUT` request (e.g., updating an asset).
2. Query the `audit_logs` table in PostgreSQL:
   ```sql
   SELECT * FROM audit_logs ORDER BY timestamp DESC;
   ```
3. Confirm the user ID, tenant ID, and payload are correctly captured.

### 3. Market & Interconnection Monitor
The `market-service` automatically monitors the ES-FR link:
1. Check the `market-service` logs in the Aspire Dashboard.
2. Look for `Checking ES-FR Interconnection Flows...`.
3. If saturation is >90% (simulated in mock), you will see: `ALERT: ES-FR Interconnection Saturated. Price decoupling highly likely.`
4. Verify the API response in `Emma.Api` via `/market/summary` includes the `MarketWarning` and current **REData** pricing.

### 4. Solar Generation Prediction
The `solar-forecaster` (Python) runs every 6 hours or can be triggered manually:
1. **Manual Trigger:** Call the FastAPI endpoint:
   ```bash
   curl -X POST http://localhost:8000/predict
   ```
2. **Verify Output:** Check the `solar-forecaster` logs for `Published predictions to Kafka`.
3. **Kafka:** Use a Kafka consumer (or Kafka UI) to listen to the `solar-predictions` topic.

### 5. Secret Management
Verify that no keys are hardcoded:
1. Check `src/Emma.Identity/appsettings.json` and `src/Emma.Api/appsettings.json`.
2. Notice that `Jwt:Key` is missing. It is injected at runtime via Aspire environment variables (`Jwt__Key`).

