import os
import sys
import json
import time
import math
import random
import uuid
from datetime import datetime, timezone, timedelta
import argparse

try:
    import paho.mqtt.client as mqtt
except ImportError:
    print("Error: paho-mqtt is not installed.")
    print("Please install it using: pip install paho-mqtt")
    sys.exit(1)

# Configuration
DEFAULT_BROKER_URL = "localhost"
DEFAULT_BROKER_PORT = 1883
TOPIC = "telemetry_raw"

class Asset:
    def __init__(self, asset_id, asset_type, location):
        self.asset_id = asset_id
        self.asset_type = asset_type # 'inverter' or 'charger'
        self.location = location
        self.energy_total_kwh = random.uniform(100, 5000)
        
        # Virtual State
        self.virtual_hour = 6.0 # Start at 06:00
        self.is_charging = False
        self.charging_ticks_remaining = 0

    def generate_telemetry(self, timestamp_override=None):
        if timestamp_override:
            # Historical Mode: strict alignment to passed time
            timestamp = timestamp_override
            # Virtual hour follows the hour of the timestamp
            # We want solar to look correct (day/night)
            self.virtual_hour = timestamp.hour + (timestamp.minute / 60.0)
            ts_str = timestamp.isoformat().replace("+00:00", "Z")
        else:
            # Real-time Mode: existing logic (fast forward or real-time simulation)
            # Keeping the fast-forward 0.5 per tick logic for "Live Demo" feel
            self.virtual_hour = (self.virtual_hour + 0.5) % 24
            ts_str = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
            
        event_id = str(uuid.uuid4())
        
        measurements = self._generate_measurements()
        
        # Apply random noise (+/- 2%)
        for key, value in measurements.items():
            if isinstance(value, (int, float)):
                noise = random.uniform(-0.02, 0.02)
                measurements[key] = value * (1 + noise)

        payload = {
            "header": {
                "event_id": event_id,
                "version": "1.0",
                "asset_id": self.asset_id
            },
            "location": self.location,
            "measurements": measurements,
            "timestamp": ts_str,
            # debug field to see virtual time
            "virtual_time_hour": float(f"{self.virtual_hour:.2f}") 
        }
        return payload

    def _generate_measurements(self):
        measurements = {}
        
        if self.asset_type == 'inverter':
            # Simulate solar power curve based on VIRTUAL hour
            hour = self.virtual_hour
            
            # Simple daylight simulation (6:00 to 20:00)
            if 6 <= hour <= 20:
                normalized_time = (hour - 6) / (20 - 6) * math.pi
                peak_power = 50.0 
                # Add some random cloud coverage factor (0.8 to 1.0)
                cloud_factor = random.uniform(0.8, 1.0)
                power_kw = peak_power * math.sin(normalized_time) * cloud_factor
            else:
                power_kw = 0.0
                
            measurements['power_kw'] = max(0.0, power_kw)
            
            # Update energy total 
            # We assume each tick represents 0.5 virtual hours for the simulation curve, 
            # but physically only 10s passed. 
            # To be realistic for "Total Energy", we should stick to physical time accumulation 
            # otherwise we'd generate massive energy in seconds.
            # So: Energy += Power (kW) * (10s / 3600)
            measurements['energy_total_kwh'] = self.energy_total_kwh + (measurements['power_kw'] * (10/3600))
            self.energy_total_kwh = measurements['energy_total_kwh']
            
            # Inverter temp follows power + random
            base_temp = 25.0 + (measurements['power_kw'] / 2.0)
            measurements['inverter_temp_c'] = base_temp + random.uniform(-2, 2)

        elif self.asset_type == 'charger':
            # State Machine for Charging
            if self.is_charging:
                self.charging_ticks_remaining -= 1
                if self.charging_ticks_remaining <= 0:
                    self.is_charging = False
            else:
                # 20% chance to start charging if idle
                if random.random() < 0.2:
                    self.is_charging = True
                    self.charging_ticks_remaining = random.randint(3, 10) # Charge for 3-10 ticks

            if self.is_charging:
                power_kw = 11.0 # 11 kW charger
            else:
                power_kw = 0.0
                
            measurements['power_kw'] = power_kw
            
            measurements['energy_total_kwh'] = self.energy_total_kwh + (power_kw * (10/3600))
            self.energy_total_kwh = measurements['energy_total_kwh']
            
        return measurements

def run_simulation(broker_url, broker_port, mode):
    # Define Locations
    locations = {
        "madrid":  {"latitude": 40.4168, "longitude": -3.7038, "market_zone": "BZN|ES", "country_code": "ES"},
        "malaga":  {"latitude": 36.7213, "longitude": -4.4214, "market_zone": "BZN|ES", "country_code": "ES"},
        "granada": {"latitude": 37.1773, "longitude": -3.5986, "market_zone": "BZN|ES", "country_code": "ES"},
        "sevilla": {"latitude": 37.3891, "longitude": -5.9845, "market_zone": "BZN|ES", "country_code": "ES"},
        "albacete": {"latitude": 38.9943, "longitude": -1.8585, "market_zone": "BZN|ES", "country_code": "ES"}
    }

    # Create Assets spread across locations
    assets = []
    
    # Madrid (Keep existing)
    assets.append(Asset("INV-ES-MAD-001", "inverter", locations["madrid"]))
    assets.append(Asset("EV-ES-MAD-002", "charger", locations["madrid"]))
    
    # Malaga
    assets.append(Asset("INV-ES-MAL-001", "inverter", locations["malaga"]))
    assets.append(Asset("EV-ES-MAL-002", "charger", locations["malaga"]))
    
    # Granada
    assets.append(Asset("INV-ES-GRA-001", "inverter", locations["granada"]))
    
    # Sevilla
    assets.append(Asset("INV-ES-SEV-001", "inverter", locations["sevilla"]))
    assets.append(Asset("EV-ES-SEV-001", "charger", locations["sevilla"]))
    
    # Albacete
    assets.append(Asset("INV-ES-ALB-001", "inverter", locations["albacete"]))

    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, f"energy_simulator_{uuid.uuid4()}")
    
    try:
        print(f"Connecting to MQTT broker at {broker_url}:{broker_port}...")
        client.connect(broker_url, broker_port)
    except Exception as e:
        print(f"Failed to connect: {e}")
        return

    if mode == "history":
        print("Generating historical data for the last 30 days...")
        # Generate 30 days of data, 1 hour step
        end_time = datetime.now(timezone.utc)
        start_time = end_time - timedelta(days=30)
        
        current_time = start_time
        total_messages = 0
        
        while current_time <= end_time:
            for asset in assets:
                telemetry = asset.generate_telemetry(timestamp_override=current_time)
                client.publish(TOPIC, json.dumps(telemetry))
                total_messages += 1
            
            # Step 1 hour
            current_time += timedelta(hours=1)
            
            if total_messages % 100 == 0:
                print(f"Generated data up to {current_time.isoformat()}... ({total_messages} msgs)")
                
        print(f"Historical data generation complete. Sent {total_messages} messages.")
        client.disconnect()
        return

    # Real-time Simulation Mode
    print("Starting real-time simulation. Press Ctrl+C to stop.")
    client.loop_start()
    
    try:
        while True:
            for asset in assets:
                telemetry = asset.generate_telemetry()
                client.publish(TOPIC, json.dumps(telemetry))
                print(f"[{datetime.now().time()}] Sent {asset.asset_id}: {telemetry['measurements']['power_kw']:.2f}kW")

            time.sleep(10)
            
    except KeyboardInterrupt:
        print("\nSimulation stopped.")
    finally:
        client.loop_stop()
        client.disconnect()

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Energy Asset Digital Twin Simulator")
    parser.add_argument("--url", type=str, help="MQTT Broker URL", default=os.getenv("MQTT_BROKER_URL", DEFAULT_BROKER_URL))
    parser.add_argument("--port", type=int, help="MQTT Port", default=None)
    parser.add_argument("--mode", type=str, choices=["live", "history"], default="live", help="Simulation mode")
    
    args = parser.parse_args()
    
    broker_url = args.url
    broker_port = args.port
    
    if "://" in broker_url:
        try:
            from urllib.parse import urlparse
            parsed = urlparse(broker_url)
            broker_url = parsed.hostname
            if parsed.port:
                broker_port = parsed.port
        except Exception:
            pass
            
    if broker_port is None:
        broker_port = int(os.getenv("MQTT_BROKER_PORT", DEFAULT_BROKER_PORT))
    
    run_simulation(broker_url, broker_port, args.mode)
