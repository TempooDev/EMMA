### Distributed Energy Management System (DEMS) & Real-Time Market Intelligence

**EMMA** is a distributed software platform designed for monitoring, analyzing, and optimizing energy assets in the European market. The platform solves the challenge of **price decoupling** and **renewable energy intermittency**, allowing energy communities and industrial operators to make decisions based on real-time data.

## 🚀 Project Purpose

In a market where prices can range from €130/MWh in France to negative values in Spain, EMMA acts as a centralized brain that:

- **Synchronizes:** Ingests massive telemetry from IoT assets (solar panels, batteries, EV chargers) using lightweight protocols.
- **Analyzes:** Processes data from the European market operator (ENTSO-E) to identify opportunities for **energy arbitrage**.
- **Optimizes:** Automates asset consumption when the grid has surpluses (negative prices), reducing the carbon footprint and operating costs.

## 🛠️ Technology Stack & Architecture

Designed with a focus on **decoupled microservices** and high availability:

- **Ingestion Engine:** Microservices in Go/Node.js processing messages via **MQTT/NATS**.
- **Data Core:** Hybrid architecture with **PostgreSQL** for business logic and **TimescaleDB** for massive time series storage with native compression.

- **Market Intelligence:** Integration with the **ENTSO-E** API for monitoring intraday prices and interconnection congestion.
- **DevOps:** Orchestrated deployment using **Docker** and real-time observability with **Grafana**.

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
Open the **Aspire Dashboard** (link provided in terminal) to monitor all microservices.

### 2. Audit Logging
To verify that sensitive actions are tracked:
1. Use the **Swagger UI** in `emma-api` to perform a `POST` or `PUT` request (e.g., updating an asset).
2. Query the `audit_logs` table in PostgreSQL:
   ```sql
   SELECT * FROM audit_logs ORDER BY timestamp DESC;
   ```
3. Confirm the user ID, tenant ID, and payload are correctly captured.

### 3. ENTSO-E Interconnection Monitor
The `market-service` automatically monitors the ES-FR link:
1. Check the `market-service` logs in the Aspire Dashboard.
2. Look for `Checking ES-FR Interconnection Flows...`.
3. If saturation is >90% (simulated in mock), you will see: `ALERT: ES-FR Interconnection Saturated. Price decoupling highly likely.`
4. Verify the API response in `emma-api` via `/market/summary` includes the `MarketWarning`.

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
1. Check `src/Emma.Identity/appsettings.json` and `src/EMMA.Api/appsettings.json`.
2. Notice that `Jwt:Key` is missing. It is injected at runtime via Aspire environment variables (`Jwt__Key`).

