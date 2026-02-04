import {
    ComposedChart,
    Line,
    Area,
    Bar,
    XAxis,
    YAxis,
    CartesianGrid,
    Tooltip,
    Legend,
    ResponsiveContainer,
    Cell
} from 'recharts';

interface EnergyData {
    time: string;
    timestamp: number;
    powerKw: number;
    pricePerMwh: number;
}

interface ArbitrageCorrelationChartProps {
    data: EnergyData[];
    formatTick: (unixTime: number) => string;
}

export default function ArbitrageCorrelationChart({ data, formatTick }: ArbitrageCorrelationChartProps) {
    return (
        <div style={{ height: '500px', width: '100%', padding: '10px' }}>
            <ResponsiveContainer>
                <ComposedChart data={data} margin={{ top: 20, right: 30, left: 20, bottom: 20 }}>
                    <defs>
                        <linearGradient id="colorPrice" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="5%" stopColor="#ff7300" stopOpacity={0.3} />
                            <stop offset="95%" stopColor="#ff7300" stopOpacity={0} />
                        </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" stroke="#333" vertical={false} />
                    <XAxis
                        dataKey="timestamp"
                        type="number"
                        domain={['dataMin', 'dataMax']}
                        tickFormatter={formatTick}
                        stroke="#888"
                        fontSize={12}
                    />
                    <YAxis
                        yAxisId="price"
                        orientation="left"
                        stroke="#ff7300"
                        fontSize={12}
                        label={{ value: 'Precio (€/MWh)', angle: -90, position: 'insideLeft', fill: '#ff7300', fontSize: 13 }}
                    />
                    <YAxis
                        yAxisId="power"
                        orientation="right"
                        stroke="#8884d8"
                        fontSize={12}
                        label={{ value: 'Consumo (kW)', angle: 90, position: 'insideRight', fill: '#8884d8', fontSize: 13 }}
                    />
                    <Tooltip
                        labelFormatter={(label) => new Date(label).toLocaleString()}
                        contentStyle={{ background: '#1e1e1e', border: '1px solid #444', borderRadius: '8px', color: '#eee' }}
                        itemStyle={{ fontSize: '12px' }}
                    />
                    <Legend wrapperStyle={{ paddingTop: '20px' }} />

                    {/* Price Area (Background Layer) */}
                    <Area
                        yAxisId="price"
                        type="monotone"
                        dataKey="pricePerMwh"
                        stroke="#ff7300"
                        fillOpacity={1}
                        fill="url(#colorPrice)"
                        name="Precio Mercado (€)"
                        strokeWidth={2}
                    />

                    {/* Consumption Bars (Foreground Layer) */}
                    <Bar
                        yAxisId="power"
                        dataKey="powerKw"
                        name="Consumo Comunitario (kW)"
                        radius={[4, 4, 0, 0]}
                    >
                        {data.map((entry, index) => (
                            <Cell
                                key={`cell-${index}`}
                                fill={entry.pricePerMwh <= 0 ? '#4ec9b0' : '#8884d8'}
                                fillOpacity={entry.pricePerMwh <= 0 ? 0.9 : 0.6}
                            />
                        ))}
                    </Bar>

                    {/* Reference line at 0 price for clarity */}
                    <Line yAxisId="price" type="monotone" dataKey={0} stroke="#666" strokeDasharray="5 5" dot={false} activeDot={false} legendType="none" />
                </ComposedChart>
            </ResponsiveContainer>

            <div style={{ textAlign: 'center', marginTop: '10px', fontSize: '13px', color: '#888' }}>
                <span style={{ display: 'inline-block', width: '12px', height: '12px', background: '#4ec9b0', borderRadius: '2px', marginRight: '5px' }}></span>
                Consumo en Horas Valle (Prc ≤ 0)
                <span style={{ display: 'inline-block', width: '12px', height: '12px', background: '#8884d8', borderRadius: '2px', marginLeft: '20px', marginRight: '5px' }}></span>
                Consumo Estándar
            </div>
        </div>
    );
}
