import os
import json
import logging
import threading
from datetime import datetime, timedelta
import pandas as pd
import numpy as np
import psycopg2
from confluent_kafka import Consumer, Producer, KafkaError
from fastapi import FastAPI

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("optimizer-engine")

app = FastAPI(title="EMMA Load-Shifting Optimizer")

# Configuration
DB_CONNECTION = os.getenv("ConnectionStrings__emma-db")
KAFKA_BROKERS = os.getenv("ConnectionStrings__messaging")
SOLAR_TOPIC = "solar-predictions"
COMMAND_TOPIC = "asset-commands"

# State
last_solar_predictions = []

@app.get("/health")
def health():
    return {"status": "ok"}

def get_price_forecast():
    if not DB_CONNECTION:
        return pd.DataFrame()
    try:
        conn = psycopg2.connect(DB_CONNECTION)
        query = """
            SELECT time, price 
            FROM market_prices 
            WHERE time >= NOW() AND time < NOW() + INTERVAL '24 hours'
            ORDER BY time ASC;
        """
        df = pd.read_sql(query, conn)
        conn.close()
        return df
    except Exception as e:
        logger.error(f"Error fetching prices: {e}")
        return pd.DataFrame()

def save_schedule(target_time, action, reason):
    if not DB_CONNECTION:
        return
    try:
        conn = psycopg2.connect(DB_CONNECTION)
        cur = conn.cursor()
        cur.execute(
            "INSERT INTO optimization_schedules (target_hour, action, reason) VALUES (%s, %s, %s)",
            (target_time, action, reason)
        )
        conn.commit()
        cur.close()
        conn.close()
    except Exception as e:
        logger.error(f"Error saving schedule: {e}")

def run_optimization():
    global last_solar_predictions
    if not last_solar_predictions:
        logger.warning("No solar predictions available for optimization")
        return

    logger.info("Running optimization algorithm...")
    
    # 1. Prepare Solar Data
    df_solar = pd.DataFrame(last_solar_predictions)
    df_solar['timestamp'] = pd.to_datetime(df_solar['timestamp'])
    
    # 2. Fetch Price Forecast
    df_prices = get_price_forecast()
    if df_prices.empty:
        logger.warning("No price forecast available")
        return

    df_prices['time'] = pd.to_datetime(df_prices['time'])
    
    # 3. Merge (nearest match)
    # Ensure timezone awareness
    df_solar['timestamp'] = df_solar['timestamp'].dt.tz_localize(None)
    df_prices['time'] = df_prices['time'].dt.tz_localize(None)
    
    df = pd.merge_asof(
        df_solar.sort_values('timestamp'), 
        df_prices.sort_values('time'), 
        left_on='timestamp', 
        right_on='time', 
        direction='nearest'
    )
    
    # 4. Optimization Logic:
    # Target: 5 hours of charging (Load Shifting)
    # Priorities: 1. Negative Prices, 2. Highest Solar, 3. Lowest Prices
    
    df['score'] = (df['predicted_yield_kwh'] * 0.5) - (df['price'] * 0.5)
    
    # Pick top 5 slots
    top_slots = df.nlargest(5, 'score')
    
    p = Producer({'bootstrap.servers': KAFKA_BROKERS})
    
    for _, row in top_slots.iterrows():
        reason = "SOLAR_SURPLUS" if row['predicted_yield_kwh'] > 1.0 else "LOW_PRICE"
        if row['price'] < 0:
            reason = "NEGATIVE_PRICE"
            
        logger.info(f"Scheduling START_CHARGING at {row['timestamp']} due to {reason}")
        
        # Save to DB
        save_schedule(row['timestamp'], "START_CHARGING", reason)
        
        # In a real system, we'd use a scheduler (like Celery or APScheduler) 
        # to emit this EXACTLY at row['timestamp'].
        # For the demo, we emit a "Planned" event.
        message = {
            "command": "START_CHARGING",
            "target_assets": ["flexible_load"],
            "scheduled_time": row['timestamp'].isoformat(),
            "reason": reason
        }
        p.produce(COMMAND_TOPIC, json.dumps(message).encode('utf-8'))
    
    p.flush()

def kafka_consumer_loop():
    if not KAFKA_BROKERS:
        return
        
    c = Consumer({
        'bootstrap.servers': KAFKA_BROKERS,
        'group.id': 'optimizer-group',
        'auto.offset.reset': 'earliest'
    })

    c.subscribe([SOLAR_TOPIC])

    while True:
        msg = c.poll(1.0)
        if msg is None: continue
        if msg.error():
            if msg.error().code() == KafkaError._PARTITION_EOF: continue
            else:
                logger.error(msg.error())
                break

        try:
            data = json.loads(msg.value().decode('utf-8'))
            global last_solar_predictions
            last_solar_predictions = data.get("predictions", [])
            logger.info(f"Received {len(last_solar_predictions)} solar predictions. Triggering optimization.")
            run_optimization()
        except Exception as e:
            logger.error(f"Error processing solar prediction: {e}")

    c.close()

@app.on_event("startup")
def startup_event():
    thread = threading.Thread(target=kafka_consumer_loop, daemon=True)
    thread.start()

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8001)
