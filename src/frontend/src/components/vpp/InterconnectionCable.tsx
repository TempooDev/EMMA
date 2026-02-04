import React from 'react';

interface InterconnectionCableProps {
    flowMw: number;
    saturation: number;
    direction: string;
}

const InterconnectionCable: React.FC<InterconnectionCableProps> = ({ flowMw, saturation, direction }) => {
    const safeSaturation = saturation ?? 0;
    const safeFlow = flowMw ?? 0;
    const isSaturated = safeSaturation > 90;

    return (
        <div style={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            gap: '1rem',
            position: 'relative',
            width: '200px'
        }}>
            <div style={{
                fontSize: '0.9rem',
                fontWeight: 'bold',
                color: isSaturated ? '#ff4b2b' : '#aaa',
                textTransform: 'uppercase'
            }}>
                {direction || 'N/A'} | {safeFlow.toFixed(0)} MW
            </div>

            <div style={{
                height: '8px',
                width: '100%',
                background: '#333',
                borderRadius: '4px',
                position: 'relative',
                overflow: 'hidden'
            }}>
                <div style={{
                    position: 'absolute',
                    height: '100%',
                    width: '40px',
                    background: isSaturated ? '#ff4b2b' : '#00c6ff',
                    borderRadius: '4px',
                    boxShadow: isSaturated ? '0 0 15px #ff4b2b' : '0 0 10px #00c6ff',
                    animation: `flow ${2 / (saturation / 100 + 0.1)}s infinite linear`,
                    left: direction === 'ES->FR' ? '0' : 'auto',
                    right: direction === 'FR->ES' ? '0' : 'auto'
                }} />
            </div>

            {isSaturated && (
                <div style={{
                    color: '#ff4b2b',
                    fontSize: '0.8rem',
                    fontWeight: 'bold',
                    animation: 'pulse 1s infinite',
                    marginTop: '0.5rem'
                }}>
                    ⚠️ INTERCONEXIÓN AL LÍMITE ({safeSaturation.toFixed(1)}%)
                </div>
            )}

            <style>{`
        @keyframes flow {
          0% { transform: translateX(-100%)}
          100% { transform: translateX(400%)}
        }
        @keyframes pulse {
          0% { opacity: 1; transform: scale(1); }
          50% { opacity: 0.5; transform: scale(1.05); }
          100% { opacity: 1; transform: scale(1); }
        }
      `}</style>
        </div>
    );
};

export default InterconnectionCable;
