import { useState, useEffect } from 'react';
import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import L from 'leaflet';

// Custom DivIcon to avoid asset loading issues
const createCustomIcon = (active: boolean) => L.divIcon({
    className: 'custom-icon',
    html: `<div style="
        background-color: ${active ? '#4caf50' : '#9e9e9e'};
        width: 12px;
        height: 12px;
        border-radius: 50%;
        border: 2px solid white;
        box-shadow: 0 0 4px rgba(0,0,0,0.4);
    "></div>`,
    iconSize: [16, 16],
    iconAnchor: [8, 8],
    popupAnchor: [0, -10]
});

interface DeviceStatus {
    deviceId: string;
    latitude: number;
    longitude: number;
    currentPowerKw: number;
    lastUpdated: string;
}

interface MapDashboardProps {
    token: string;
}

export function MapDashboard({ token }: MapDashboardProps) {
    const [devices, setDevices] = useState<DeviceStatus[]>([]);
    const [loading, setLoading] = useState(false);

    const fetchDevices = async () => {
        setLoading(true);
        try {
            const response = await fetch('/api/dashboard/devices-status', {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });
            if (response.ok) {
                const data = await response.json();
                console.log('Device Status:', data);
                setDevices(data);
            }
        } catch (error) {
            console.error('Failed to fetch devices', error);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchDevices();
        const interval = setInterval(fetchDevices, 30000); // 30s refresh
        return () => clearInterval(interval);
    }, []);

    // Default center (Madrid)
    const center: [number, number] = [40.4168, -3.7038];

    return (
        <div className="map-container" style={{ height: 'calc(100vh - 100px)', width: '100%', position: 'relative' }}>
            {loading && <div style={{ position: 'absolute', top: 10, right: 10, zIndex: 1000, background: 'rgba(0,0,0,0.7)', color: 'white', padding: '5px 10px', borderRadius: '4px' }}>Refreshing...</div>}

            <MapContainer center={center} zoom={6} scrollWheelZoom={true} style={{ height: '100%', width: '100%' }}>
                <TileLayer
                    attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                    url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                />
                {devices.map(device => (
                    device.latitude && device.longitude ? (
                        <Marker
                            key={device.deviceId}
                            position={[device.latitude, device.longitude]}
                            icon={createCustomIcon((device.currentPowerKw || 0) > 0)}
                        >
                            <Popup>
                                <strong>{device.deviceId}</strong><br />
                                Power: {device.currentPowerKw?.toFixed(2) ?? 0} kW<br />
                                Updated: {new Date(device.lastUpdated).toLocaleTimeString()}
                            </Popup>
                        </Marker>
                    ) : null
                ))}
            </MapContainer>

            <button onClick={fetchDevices} style={{
                position: 'absolute', bottom: 20, left: 20, zIndex: 1000,
                padding: '10px 20px', cursor: 'pointer', background: '#0078d4', color: 'white', border: 'none', borderRadius: '4px',
                boxShadow: '0 2px 5px rgba(0,0,0,0.3)'
            }}>
                Refresh Map
            </button>
        </div>
    );
}
