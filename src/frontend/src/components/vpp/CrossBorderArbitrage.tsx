import React, { useEffect, useState } from 'react';
import MarketCard from './MarketCard';
import InterconnectionCable from './InterconnectionCable';

interface ArbitrageData {
    priceEs: number;
    priceFr: number;
    physicalFlowMw: number;
    ntcCmw: number;
    saturationPercentage: number;
    flowDirection: string;
}

const CrossBorderArbitrage: React.FC = () => {
    const [data, setData] = useState<ArbitrageData | null>(null);

    useEffect(() => {
        const fetchData = async () => {
            try {
                const response = await fetch('/api/dashboard/arbitrage');
                const json = await response.json();
                setData(json);
            } catch (error) {
                console.error('Error fetching arbitrage data:', error);
            }
        };

        fetchData();
        const interval = setInterval(fetchData, 30000); // 30s
        return () => clearInterval(interval);
    }, []);

    if (!data) return <div style={{ color: '#aaa' }}>Loading Arbitrage Data...</div>;

    return (
        <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: '2rem',
            padding: '4rem',
            background: '#0f0f0f',
            borderRadius: '2rem',
            margin: '2rem 0',
            fontFamily: 'system-ui, sans-serif'
        }}>
            <MarketCard country="Spain" price={data.priceEs} currency="EUR" />

            <InterconnectionCable
                flowMw={data.physicalFlowMw}
                saturation={data.saturationPercentage}
                direction={data.flowDirection}
            />

            <MarketCard country="France" price={data.priceFr} currency="EUR" />
        </div>
    );
};

export default CrossBorderArbitrage;
