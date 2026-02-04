import { useState, useEffect } from 'react';
import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

// Fix for default marker icons in Leaflet with Webpack/Vite
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png';
import markerIcon from 'leaflet/dist/images/marker-icon.png';
import markerShadow from 'leaflet/dist/images/marker-shadow.png';

delete (L.Icon.Default.prototype as any)._getIconUrl;
L.Icon.Default.mergeOptions({
    iconUrl: markerIcon,
    iconRetinaUrl: markerIcon2x,
    shadowUrl: markerShadow,
});

interface DeviceStatus {
    deviceId: string;
    modelName: string;
    latitude: number;
    longitude: number;
    currentPowerKw: number;
    temperature: number;
    lastUpdated: string;
    isChargingNegativePrice: boolean;
}

interface AssetMapProps {
    token: string;
}

// Custom icons based on asset type
const getIcon = (modelName: string, isChargingNegative: boolean) => {
    const color = isChargingNegative ? '#4ec9b0' : '#0078d4';
    const html = `
    <div class="custom-marker-container">
      ${isChargingNegative ? '<div class="pulse-aura"></div>' : ''}
      <div class="marker-dot" style="background-color: ${color};">
        <span style="font-size: 12px;">${modelName.toLowerCase().includes('charger') ? '🔌' : modelName.toLowerCase().includes('inverter') ? '☀️' : '🔋'}</span>
      </div>
    </div>
  `;

    return L.divIcon({
        className: 'custom-div-icon',
        html: html,
        iconSize: [30, 30],
        iconAnchor: [15, 15]
    });
};

export default function AssetMap({ token }: AssetMapProps) {
    const [devices, setDevices] = useState<DeviceStatus[]>([]);

    const fetchDevices = async () => {
        try {
            const response = await fetch('/api/dashboard/devices-status', {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });
            if (response.ok) {
                const data = await response.json();
                setDevices(data);
            }
        } catch (error) {
            console.error('Failed to fetch devices for map', error);
        }
    };

    useEffect(() => {
        fetchDevices();
        const interval = setInterval(fetchDevices, 10000);
        return () => clearInterval(interval);
    }, [token]);

    // Center on Spain default
    const center: [number, number] = [37.1773, -3.5986]; // Granada/Malaga area

    return (
        <div style={{ height: '500px', width: '100%', borderRadius: '12px', overflow: 'hidden', border: '1px solid #333' }}>
            <style>{`
        .custom-marker-container {
          position: relative;
          width: 30px;
          height: 30px;
          display: flex;
          align-items: center;
          justify-content: center;
        }
        .marker-dot {
          width: 24px;
          height: 24px;
          border-radius: 50%;
          border: 2px solid white;
          display: flex;
          align-items: center;
          justify-content: center;
          box-shadow: 0 2px 5px rgba(0,0,0,0.3);
          z-index: 2;
        }
        .pulse-aura {
          position: absolute;
          width: 40px;
          height: 40px;
          background-color: rgba(78, 201, 176, 0.6);
          border-radius: 50%;
          animation: map-pulse 1.5s infinite;
          z-index: 1;
        }
        @keyframes map-pulse {
          0% { transform: scale(0.6); opacity: 1; }
          100% { transform: scale(1.5); opacity: 0; }
        }
        .leaflet-container {
          background: #1a1a1a !important;
        }
        .leaflet-tile {
          filter: invert(100%) hue-rotate(180deg) brightness(95%) contrast(90%);
        }
      `}</style>
            <MapContainer center={center} zoom={6} style={{ height: '100%', width: '100%' }}>
                <TileLayer
                    attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                    url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                />
                {devices.map((device) => (
                    device.latitude && device.longitude ? (
                        <Marker
                            key={device.deviceId}
                            position={[device.latitude, device.longitude]}
                            icon={getIcon(device.modelName || '', device.isChargingNegativePrice)}
                        >
                            <Popup>
                                <div style={{ minWidth: '150px' }}>
                                    <strong style={{ display: 'block', marginBottom: '5px' }}>{device.deviceId}</strong>
                                    <div style={{ fontSize: '12px', color: '#666' }}>{device.modelName}</div>
                                    <hr style={{ margin: '8px 0', border: 'none', borderTop: '1px solid #eee' }} />
                                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px' }}>
                                        <span>Potencia:</span>
                                        <span style={{ fontWeight: 'bold', color: device.currentPowerKw > 0 ? '#ff7300' : '#4ec9b0' }}>
                                            {device.currentPowerKw?.toFixed(2)} kW
                                        </span>
                                    </div>
                                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                                        <span>Temp:</span>
                                        <span style={{ fontWeight: 'bold' }}>{device.temperature?.toFixed(1)}°C</span>
                                    </div>
                                    {device.isChargingNegativePrice && (
                                        <div style={{ marginTop: '8px', padding: '4px', background: 'rgba(78, 201, 176, 0.1)', color: '#4ec9b0', fontSize: '10px', textAlign: 'center', borderRadius: '4px', fontWeight: 'bold' }}>
                                            ⚡ MODO AHORRO ACTIVO
                                        </div>
                                    )}
                                </div>
                            </Popup>
                        </Marker>
                    ) : null
                ))}
            </MapContainer>
        </div>
    );
}
