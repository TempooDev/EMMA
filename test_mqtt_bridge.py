import sys
import json
import time

try:
    import paho.mqtt.client as mqtt
except ImportError:
    print("Error: paho-mqtt is not installed.")
    print("Please install it using: pip install paho-mqtt")
    sys.exit(1)

def test_bridge(port):
    broker = "localhost"
    topic = "telemetry-raw"
    
    print(f"Connecting to MQTT broker at {broker}:{port}...")
    
    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, "test_publisher")
    
    try:
        client.connect(broker, int(port))
    except Exception as e:
        print(f"Failed to connect: {e}")
        return

    payload = {
        "timestamp": time.time(),
        "type": "test",
        "value": 123.45,
        "message": "Hello from test script"
    }
    
    print(f"Publishing to topic '{topic}': {json.dumps(payload)}")
    
    info = client.publish(topic, json.dumps(payload))
    info.wait_for_publish()
    
    if info.rc == mqtt.MQTT_ERR_SUCCESS:
        print("Message published successfully!")
    else:
        print(f"Failed to publish message. Return code: {info.rc}")

    client.disconnect()

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python3 test_mqtt_bridge.py <mqtt_port>")
        sys.exit(1)
        
    port = sys.argv[1]
    test_bridge(port)
