import React from 'react';

interface MarketCardProps {
    country: string;
    price: number;
    currency: string;
}

const MarketCard: React.FC<MarketCardProps> = ({ country, price, currency }) => {
    const getGradient = (p: number) => {
        if (p < 10) return 'linear-gradient(135deg, #00c6ff, #0072ff)'; // Blue/Green (Cheap)
        if (p > 100) return 'linear-gradient(135deg, #f12711, #f5af19)'; // Orange/Red (Expensive)
        return 'linear-gradient(135deg, #3a7bd5, #3a6073)'; // Neutral
    };

    return (
        <div style={{
            background: getGradient(price),
            padding: '2rem',
            borderRadius: '1.5rem',
            color: 'white',
            minWidth: '250px',
            boxShadow: '0 10px 30px rgba(0,0,0,0.3)',
            transition: 'all 0.5s ease',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            textAlign: 'center'
        }}>
            <h2 style={{ fontSize: '1.2rem', textTransform: 'uppercase', letterSpacing: '2px', opacity: 0.8 }}>{country} Market</h2>
            <div style={{ fontSize: '4rem', fontWeight: 'bold', margin: '1rem 0' }}>
                {price.toFixed(2)}
            </div>
            <div style={{ fontSize: '1.2rem', opacity: 0.9 }}>{currency}/MWh</div>
        </div>
    );
};

export default MarketCard;
