import { useState, useEffect } from 'react';
import {
    AreaChart,
    Area,
    XAxis,
    YAxis,
    CartesianGrid,
    Tooltip,
    ResponsiveContainer,
    ReferenceArea,
    Label
} from 'recharts';

interface ForecastData {
    time: string;
    pricePerMwh: number;
    timestamp: number;
}

interface PredictiveTimelineProps {
    token: string;
}

export default function PredictiveTimeline({ token }: PredictiveTimelineProps) {
    const [data, setData] = useState<ForecastData[]>([]);
    const [loading, setLoading] = useState(true);

    const fetchForecast = async () => {
        try {
            const response = await fetch('/api/dashboard/price-forecast', {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });
            if (response.ok) {
                const rawData = await response.json();
                const mappedData = rawData.map((d: any) => ({
                    ...d,
                    timestamp: new Date(d.time).getTime()
                }));
                setData(mappedData);
            }
        } catch (error) {
            console.error('Failed to fetch forecast', error);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchForecast();
    }, [token]);

    // Derive "Smart Badges" and Action Zones
    // For demonstration, let's say the 3 cheapest hours are action zones
    const sortedByPrice = [...data].sort((a, b) => a.pricePerMwh - b.pricePerMwh);
    const cheapestHours = sortedByPrice.slice(0, 3).map(d => d.timestamp);

    // Find solar peak (mid-day roughly)
    const solarPeak = data.find(d => {
        const hour = new Date(d.time).getHours();
        return hour === 13; // 1 PM
    });

    const formatHour = (ts: number) => {
        return new Date(ts).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    };

    if (loading) return <div style={{ color: 'white', textAlign: 'center', padding: '50px' }}>Loading forecast...</div>;
    if (data.length === 0) return <div style={{ color: 'white', textAlign: 'center', padding: '50px' }}>No forecast data available for next 24h.</div>;

    return (
        <div style={{ height: '100%', width: '100%', position: 'relative' }}>
            <div style={{ marginBottom: '20px', display: 'flex', gap: '15px' }}>
                <div style={{ background: 'rgba(78, 201, 176, 0.2)', border: '1px solid #4ec9b0', color: '#4ec9b0', padding: '5px 10px', borderRadius: '4px', fontSize: '12px' }}>
                    ✨ Charging Opportunity Detected
                </div>
                <div style={{ background: 'rgba(255, 115, 0, 0.2)', border: '1px solid #ff7300', color: '#ff7300', padding: '5px 10px', borderRadius: '4px', fontSize: '12px' }}>
                    ☀️ Solar Peak Preview
                </div>
            </div>

            <ResponsiveContainer width="100%" height={400}>
                <AreaChart data={data} margin={{ top: 20, right: 30, left: 0, bottom: 0 }}>
                    <defs>
                        <linearGradient id="colorPrice" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="5%" stopColor="#ff7300" stopOpacity={0.3} />
                            <stop offset="95%" stopColor="#ff7300" stopOpacity={0} />
                        </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" stroke="#333" />
                    <XAxis
                        dataKey="timestamp"
                        type="number"
                        domain={['dataMin', 'dataMax']}
                        tickFormatter={formatHour}
                        stroke="#888"
                    />
                    <YAxis stroke="#888" label={{ value: '€/MWh', angle: -90, position: 'insideLeft', fill: '#888' }} />
                    <Tooltip
                        labelFormatter={(label) => new Date(label).toLocaleString()}
                        contentStyle={{ background: '#222', border: '1px solid #444', color: '#fff' }}
                    />

                    {/* Action Zones - Shading cheapest hours */}
                    {cheapestHours.map((ts, idx) => (
                        <ReferenceArea
                            key={idx}
                            x1={ts - 1800000}
                            x2={ts + 1800000}
                            fill="rgba(78, 201, 176, 0.15)"
                            stroke="#4ec9b0"
                            strokeDasharray="3 3"
                        >
                            <Label value="⚡ CHARGE" position="top" fill="#4ec9b0" style={{ fontSize: '10px', fontWeight: 'bold' }} />
                        </ReferenceArea>
                    ))}

                    {/* Solar Peak Marker */}
                    {solarPeak && (
                        <ReferenceArea
                            x1={solarPeak.timestamp - 3600000}
                            x2={solarPeak.timestamp + 3600000}
                            fill="rgba(255, 215, 0, 0.05)"
                        >
                            <Label value="☀️ SOLAR MAX" position="insideTop" fill="#ffd700" style={{ fontSize: '10px' }} />
                        </ReferenceArea>
                    )}

                    <Area
                        type="monotone"
                        dataKey="pricePerMwh"
                        stroke="#ff7300"
                        fillOpacity={1}
                        fill="url(#colorPrice)"
                        name="Forecasted Price"
                        strokeWidth={3}
                    />
                </AreaChart>
            </ResponsiveContainer>

            <div style={{ marginTop: '20px', color: '#888', fontSize: '13px', lineHeight: '1.6' }}>
                <p>💡 <strong>EMMA Analysis:</strong> The system has identified the optimal window for battery charging between {formatHour(cheapestHours[0])} and {formatHour(cheapestHours[cheapestHours.length - 1])}.
                    Electric vehicle charging is recommended during the solar peak to maximize self-consumption.</p>
            </div>
        </div>
    );
}
