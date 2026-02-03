import { useState, useEffect } from 'react';
import {
  ComposedChart,
  Line,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
  Brush
} from 'recharts';

interface EnergyData {
  time: string;
  powerKw: number;
  pricePerMwh: number;
}

export function Dashboard() {
  const [data, setData] = useState<EnergyData[]>([]);
  const [loading, setLoading] = useState(false);

  // Default to today (start of day to end of day)
  const [startDate, setStartDate] = useState(() => {
    const d = new Date();
    d.setHours(d.getHours() - 24); // Default to last 24h
    return d.toISOString().slice(0, 16); // format for datetime-local
  });
  const [endDate, setEndDate] = useState(() => {
    const d = new Date();
    return d.toISOString().slice(0, 16);
  });

  const [bucket, setBucket] = useState('1 minute');

  const fetchData = async () => {
    setLoading(true);
    try {
      const startIso = new Date(startDate).toISOString();
      const endIso = new Date(endDate).toISOString();

      const response = await fetch(`/api/dashboard/energy-mix?start=${startIso}&end=${endIso}&bucket=${bucket}`);

      if (response.ok) {
        const rawData = await response.json();
        console.log('Raw Dashboard Data:', rawData); // Log raw data to see exact format

        if (!Array.isArray(rawData)) {
          console.error("API response is not an array:", rawData);
          setData([]);
          return;
        }

        const formattedData = rawData.map((item: any) => {
          // Handle "2026-02-02 23:00:00+00" format specifically if needed
          // But let's log the first one to be sure
          return {
            time: new Date(item.time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
            timestamp: new Date(item.time).getTime(),
            powerKw: Number(item.powerKw) || 0,
            pricePerMwh: Number(item.pricePerMwh) || 0
          };
        });

        console.log('Formatted Data (First 5):', formattedData.slice(0, 5));
        setData(formattedData);
      }
    } catch (error) {
      console.error('Failed to fetch dashboard data', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
    const interval = setInterval(fetchData, 60000);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    fetchData();
  }, [startDate, endDate, bucket]);  // Refetch when bucket changes too


  return (
    <div className="dashboard-container" style={{ padding: '20px', color: '#eee' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '15px', flexWrap: 'wrap', gap: '10px' }}>
        <h2 style={{ margin: 0 }}>Energy Mix (Grafino)</h2>
        <div style={{ display: 'flex', gap: '10px', alignItems: 'center', flexWrap: 'wrap' }}>
          <select
            value={bucket}
            onChange={e => setBucket(e.target.value)}
            style={{ padding: '6px', borderRadius: '4px', border: '1px solid #444', background: '#222', color: 'white', height: '32px' }}
            aria-label="Select Time Bucket"
          >
            <option value="5 seconds">5 Seconds</option>
            <option value="30 seconds">30 Seconds</option>
            <option value="1 minute">1 Minute</option>
            <option value="5 minutes">5 Minutes</option>
            <option value="15 minutes">15 Minutes</option>
            <option value="1 hour">1 Hour</option>
          </select>
          <label style={{ display: 'flex', alignItems: 'center', gap: '5px' }}>
            Start:
            <input
              type="datetime-local"
              value={startDate}
              onChange={e => setStartDate(e.target.value)}
              style={{ padding: '5px', borderRadius: '4px', border: '1px solid #444', background: '#222', color: 'white', height: '32px', boxSizing: 'border-box' }}
            />
          </label>
          <label style={{ display: 'flex', alignItems: 'center', gap: '5px' }}>
            End:
            <input
              type="datetime-local"
              value={endDate}
              onChange={e => setEndDate(e.target.value)}
              style={{ padding: '5px', borderRadius: '4px', border: '1px solid #444', background: '#222', color: 'white', height: '32px', boxSizing: 'border-box' }}
            />
          </label>
          <button onClick={fetchData} style={{ padding: '0 15px', height: '32px', cursor: 'pointer', background: '#0078d4', color: 'white', border: 'none', borderRadius: '4px', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            Refresh
          </button>
        </div>
      </div>

      <div style={{ height: '500px', width: '100%', background: '#1e1e1e', borderRadius: '8px', padding: '10px' }}>
        {loading && data.length === 0 ? (
          <p style={{ color: 'white', textAlign: 'center', paddingTop: '200px' }}>Loading...</p>
        ) : data.length === 0 ? (
          <p style={{ color: '#aaa', textAlign: 'center', paddingTop: '200px' }}>No data available for the selected time range.</p>
        ) : (
          <ResponsiveContainer>
            <ComposedChart data={data} margin={{ top: 10, right: 30, left: 0, bottom: 0 }}>
              <CartesianGrid stroke="#f5f5f5" strokeOpacity={0.1} />
              <XAxis
                dataKey="timestamp"
                type="number"
                domain={['dataMin', 'dataMax']}
                tickFormatter={(unixTime) => new Date(unixTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                stroke="#ccc"
                angle={-45}
                textAnchor="end"
                height={70}
                tick={{ fontSize: 12 }}
              />
              <YAxis yAxisId="left" stroke="#8884d8" label={{ value: 'Power (kW)', angle: -90, position: 'insideLeft' }} />
              <YAxis yAxisId="right" orientation="right" stroke="#ff7300" label={{ value: 'Price (€/MWh)', angle: 90, position: 'insideRight' }} />
              <Tooltip
                labelFormatter={(label) => new Date(label).toLocaleString()}
                contentStyle={{ backgroundColor: '#333', border: 'none', color: '#fff' }}
              />
              <Legend verticalAlign="top" />
              <Brush dataKey="timestamp" height={30} stroke="#8884d8" fill="#1e1e1e" tickFormatter={(unixTime) => new Date(unixTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })} />
              <Area yAxisId="left" type="monotone" dataKey="powerKw" fill="#8884d8" stroke="#8884d8" name="Power Generation" fillOpacity={0.3} />
              <Line yAxisId="right" type="monotone" dataKey="pricePerMwh" stroke="#ff7300" name="Market Price" dot={false} strokeWidth={2} />
            </ComposedChart>
          </ResponsiveContainer>
        )}
      </div>
    </div>
  );
}
