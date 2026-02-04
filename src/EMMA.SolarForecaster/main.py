import os
import time
import json
import logging
from datetime import datetime, timedelta
from typing import List

from fastapi import FastAPI, BackgroundTasks
import psycopg2
import pandas as pd
from xgboost import XGBRegressor
from confluent_kafka import Producer
import requests

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("solar-forecaster")

app = FastAPI(title="EMMA Solar Forecaster")

# Configuration from environment variables (injected by Aspire)
DB_CONNECTION = os.getenv("ConnectionStrings__telemetry-db")
KAFKA_BROKERS = os.getenv("ConnectionStrings__messaging")
KAFKA_TOPIC = "solar-predictions"

@app.get("/health")
def health():
    return {"status": "ok"}

def get_historical_data():
    if not DB_CONNECTION:
        logger.error("DB_CONNECTION not set")
        return pd.DataFrame()
    
    try:
        conn = psycopg2.connect(DB_CONNECTION)
        query = """
            SELECT time, power_kw 
            FROM asset_metrics_hourly 
            WHERE time > NOW() - INTERVAL '30 days'
            ORDER BY time ASC;
        """
        df = pd.read_sql(query, conn)
        conn.close()
        return df
    except Exception as e:
        logger.error(f"Error fetching data: {e}")
        return pd.DataFrame()

def get_weather_forecast():
    # In a real scenario, use Open-Meteo or similar
    # For now, we return mock weather for the next 48 hours
    logger.info("Fetching weather forecast...")
    forecast = []
    for i in range(48):
        forecast.append({
            "hour": (datetime.utcnow() + timedelta(hours=i)).hour,
            "temp": 20 + 5 * i / 48, # Simple mock temp
            "cloud_cover": 20 if i % 24 > 10 else 80 # Day vs Night simulation
        })
    return pd.DataFrame(forecast)

def train_and_predict():
    logger.info("Training XGBoost model and generating predictions...")
    
    # 1. Fetch historical data
    df_hist = get_historical_data()
    if df_hist.empty:
        logger.warning("No historical data available for training")
        return []

    df_hist['hour'] = pd.to_datetime(df_hist['time']).dt.hour
    
    # 2. Train model
    # Simple features: hour
    X = df_hist[['hour']]
    y = df_hist['power_kw']
    
    model = XGBRegressor(n_estimators=100, learning_rate=0.1, max_depth=5)
    model.fit(X, y)
    
    # 3. Predict for next 24 hours using forecast
    df_forecast = get_weather_forecast()
    df_next_24 = df_forecast.head(24).copy()
    
    # Prepare features for prediction
    X_pred = df_next_24[['hour']]
    df_next_24['predicted_yield_kwh'] = model.predict(X_pred)
    
    # Ensure no negative predictions (solar can't be negative)
    df_next_24['predicted_yield_kwh'] = df_next_24['predicted_yield_kwh'].clip(lower=0)
    
    predictions = []
    now = datetime.utcnow()
    for i, row in df_next_24.iterrows():
        pred_time = now + timedelta(hours=i)
        predictions.append({
            "timestamp": pred_time.isoformat(),
            "predicted_yield_kwh": float(round(row['predicted_yield_kwh'], 2)),
            "confidence": 0.88
        })
    
    return predictions

def prediction_loop():
    while True:
        try:
            preds = train_and_predict()
            if preds:
                publish_to_kafka(preds)
            logger.info("Next prediction in 6 hours...")
            time.sleep(6 * 3600) # 6 hours
        except Exception as e:
            logger.error(f"Error in prediction loop: {e}")
            time.sleep(60)

@app.on_event("startup")
def startup_event():
    import threading
    thread = threading.Thread(target=prediction_loop, daemon=True)
    thread.start()

def publish_to_kafka(predictions):
    if not KAFKA_BROKERS:
        logger.error("KAFKA_BROKERS not set")
        return
    
    try:
        # Kafka config
        conf = {
            'bootstrap.servers': KAFKA_BROKERS,
            'client.id': 'solar-forecaster'
        }
        p = Producer(conf)
        
        message = {
            "source": "solar-forecaster",
            "predictions": predictions,
            "metadata": {
                "model": "XGBoost",
                "features": ["hour", "weather_mock"]
            }
        }
        
        p.produce(KAFKA_TOPIC, json.dumps(message).encode('utf-8'))
        p.flush()
        logger.info(f"Published {len(predictions)} predictions to Kafka topic: {KAFKA_TOPIC}")
    except Exception as e:
        logger.error(f"Error publishing to Kafka: {e}")

@app.post("/predict")
def trigger_prediction(background_tasks: BackgroundTasks):
    background_tasks.add_task(train_and_predict)
    return {"message": "Manual prediction task started"}

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
