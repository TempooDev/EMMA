import { useState, useEffect } from 'react';

interface ImpactMetrics {
    totalSavingsEur: number;
    negativePriceEnergyKwh: number;
    currentPriceEurMwh: number;
}

interface ImpactCounterProps {
    token: string;
}

export default function ImpactCounter({ token }: ImpactCounterProps) {
    const [metrics, setMetrics] = useState<ImpactMetrics>({
        totalSavingsEur: 0,
        negativePriceEnergyKwh: 0,
        currentPriceEurMwh: 0
    });

    const fetchMetrics = async () => {
        try {
            const response = await fetch('/api/dashboard/impact-metrics', {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });
            if (response.ok) {
                const data = await response.json();
                setMetrics(data);
            }
        } catch (error) {
            console.error('Failed to fetch impact metrics', error);
        }
    };

    useEffect(() => {
        fetchMetrics();
        const interval = setInterval(fetchMetrics, 5000); // Poll every 5s
        return () => clearInterval(interval);
    }, [token]);

    return (
        <div style={{ display: 'flex', gap: '20px', marginBottom: '20px', flexWrap: 'wrap' }}>
            {/* Odometer-style Counter Card */}
            <div style={{
                flex: '1',
                minWidth: '280px',
                background: 'linear-gradient(135deg, #0078d4, #00bcf2)',
                borderRadius: '12px',
                padding: '20px',
                color: 'white',
                boxShadow: '0 8px 32px rgba(0, 120, 212, 0.3)',
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'center',
                alignItems: 'center',
                position: 'relative',
                overflow: 'hidden'
            }}>
                <div style={{ fontSize: '0.9rem', opacity: 0.9, marginBottom: '5px', fontWeight: 'bold', textTransform: 'uppercase', letterSpacing: '1px' }}>
                    Ahorro Acumulado (Hoy)
                </div>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: '5px' }}>
                    <span style={{ fontSize: '1.2rem', fontWeight: 'bold' }}>€</span>
                    <div style={{
                        fontSize: '2.5rem',
                        fontWeight: '900',
                        fontFamily: 'monospace',
                        letterSpacing: '2px',
                        textShadow: '0 2px 10px rgba(0,0,0,0.2)'
                    }}>
                        {metrics.totalSavingsEur.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                    </div>
                </div>
                {/* Subtle Background Icon */}
                <div style={{ position: 'absolute', right: '-10px', bottom: '-10px', fontSize: '5rem', opacity: 0.1, transform: 'rotate(-15deg)' }}>
                    💰
                </div>
            </div>

            {/* Negative Price Energy Card */}
            <div style={{
                flex: '1',
                minWidth: '220px',
                background: '#252525',
                border: '1px solid #333',
                borderRadius: '12px',
                padding: '20px',
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'center'
            }}>
                <div style={{ fontSize: '0.8rem', color: '#888', marginBottom: '5px', fontWeight: 'bold' }}>
                    EXCEDENTE APROVECHADO
                </div>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px' }}>
                    <div style={{ fontSize: '1.8rem', fontWeight: 'bold', color: '#4ec9b0' }}>
                        {metrics.negativePriceEnergyKwh.toLocaleString(undefined, { maximumFractionDigits: 1 })}
                    </div>
                    <div style={{ color: '#666', fontSize: '0.9rem' }}>kWh</div>
                </div>
                <div style={{ fontSize: '0.7rem', color: '#555', marginTop: '4px' }}>
                    Energía consumida con precios ≤ 0
                </div>
            </div>

            {/* Current Real Price Card */}
            <div style={{
                flex: '1',
                minWidth: '220px',
                background: '#252525',
                border: '1px solid #333',
                borderRadius: '12px',
                padding: '20px',
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'center'
            }}>
                <div style={{ fontSize: '0.8rem', color: '#888', marginBottom: '5px', fontWeight: 'bold' }}>
                    PRECIO MERCADO ACTUAL
                </div>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px' }}>
                    <div style={{ fontSize: '1.8rem', fontWeight: 'bold', color: metrics.currentPriceEurMwh > 0 ? '#ff7300' : '#4ec9b0' }}>
                        {metrics.currentPriceEurMwh.toFixed(2)}
                    </div>
                    <div style={{ color: '#666', fontSize: '0.9rem' }}>€/MWh</div>
                </div>
                <div style={{ fontSize: '0.7rem', color: '#555', marginTop: '4px' }}>
                    Referencia: REData (ES)
                </div>
            </div>
        </div>
    );
}
