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
import CrossBorderArbitrage from '../components/vpp/CrossBorderArbitrage';

interface EnergyData {
  time: string;
  powerKw: number;
  pricePerMwh: number;
}

export function Dashboard() {
  const [data, setData] = useState<EnergyData[]>([]);
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState<'chart' | 'arbitrage'>('chart');

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

        if (!Array.isArray(rawData)) {
          console.error("API response is not an array:", rawData);
          setData([]);
          return;
        }

        const formattedData = rawData.map((item: any) => ({
          // Handle "2026-02-02 23:00:00+00" format specifically if needed
          // But let's log the first one to be sure
          time: new Date(item.time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          timestamp: new Date(item.time).getTime(),
          powerKw: Number(item.powerKw) || 0,
          pricePerMwh: Number(item.pricePerMwh) || 0
        }));

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

  const formatTick = (unixTime: number) => {
    const start = new Date(startDate).getTime();
    const end = new Date(endDate).getTime();
    const oneDay = 24 * 60 * 60 * 1000;

    const date = new Date(unixTime);

    if (end - start > oneDay) {
      // Show Date + Time for multi-day ranges
      return date.toLocaleString([], { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
    }
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

  return (
    <div className="dashboard-container" style={{ padding: '20px', color: '#eee' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '15px', flexWrap: 'wrap', gap: '10px' }}>
        <h2 style={{ margin: 0 }}>EMMA Analytics</h2>
        <div style={{ display: 'flex', gap: '10px', alignItems: 'center', flexWrap: 'wrap' }}>
          <div style={{ background: '#222', borderRadius: '8px', padding: '4px', display: 'flex', gap: '4px' }}>
            <button
              onClick={() => setActiveTab('chart')}
              style={{ padding: '6px 12px', borderRadius: '6px', border: 'none', background: activeTab === 'chart' ? '#0078d4' : 'transparent', color: 'white', cursor: 'pointer' }}
            >
              Chart
            </button>
            <button
              onClick={() => setActiveTab('arbitrage')}
              style={{ padding: '6px 12px', borderRadius: '6px', border: 'none', background: activeTab === 'arbitrage' ? '#0078d4' : 'transparent', color: 'white', cursor: 'pointer' }}
            >
              Arbitrage
            </button>
          </div>
          <select
            value={bucket}
            onChange={e => setBucket(e.target.value)}
            style={{ padding: '6px', borderRadius: '4px', border: '1px solid #444', background: '#222', color: 'white', height: '32px' }}
          >
            <option value="5 seconds">5s</option>
            <option value="30 seconds">30s</option>
            <option value="1 minute">1m</option>
            <option value="5 minutes">5m</option>
            <option value="15 minutes">15m</option>
            <option value="1 hour">1h</option>
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

      <div style={{ minHeight: '550px', width: '100%', background: '#1e1e1e', borderRadius: '8px', padding: '20px' }}>
        {activeTab === 'chart' ? (
          <div style={{ height: '500px' }}>
            {loading && data.length === 0 ? (
              <p style={{ textAlign: 'center', paddingTop: '200px' }}>Loading...</p>
            ) : data.length === 0 ? (
              <p style={{ textAlign: 'center', paddingTop: '200px' }}>No data available.</p>
            ) : (
              <ResponsiveContainer>
                <ComposedChart data={data}>
                  <CartesianGrid stroke="#333" />
                  <XAxis dataKey="timestamp" type="number" domain={['dataMin', 'dataMax']} tickFormatter={formatTick} stroke="#888" />
                  <YAxis yAxisId="left" stroke="#8884d8" />
                  <YAxis yAxisId="right" orientation="right" stroke="#ff7300" />
                  <Tooltip labelFormatter={(label) => new Date(label).toLocaleString()} contentStyle={{ background: '#222', border: '1px solid #444' }} />
                  <Legend />
                  <Area yAxisId="left" type="monotone" dataKey="powerKw" fill="#8884d8" stroke="#8884d8" name="Power (kW)" fillOpacity={0.3} />
                  <Line yAxisId="right" type="monotone" dataKey="pricePerMwh" stroke="#ff7300" name="Price (€)" dot={false} strokeWidth={2} />
                  <Brush dataKey="timestamp" height={30} stroke="#8884d8" fill="#1e1e1e" tickFormatter={formatTick} />
                </ComposedChart>
              </ResponsiveContainer>
            )}
          </div>
        ) : (
          <CrossBorderArbitrage />
        )}
      </div>
    </div>
  );
}
