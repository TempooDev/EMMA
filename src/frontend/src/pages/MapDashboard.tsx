import React from 'react';

interface MapDashboardProps {
  token: string;
}

export const MapDashboard: React.FC<MapDashboardProps> = () => {
  return (
    <div className="dashboard-container">
      <h2>Map Dashboard</h2>
      <p>Map view coming soon...</p>
    </div>
  );
};
