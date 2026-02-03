import { useState, useEffect } from 'react'
import { Dashboard } from './pages/Dashboard';
import { MapDashboard } from './pages/MapDashboard';
import aspireLogo from '/Aspire.png'
import './App.css'

interface WeatherForecast {
  date: string
  temperatureC: number
  temperatureF: number
  summary: string
}

function App() {
  const [activeTab, setActiveTab] = useState<'weather' | 'dashboard' | 'map'>('dashboard');
  
  // Weather State
  const [weatherData, setWeatherData] = useState<WeatherForecast[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [useCelsius, setUseCelsius] = useState(false)

  const fetchWeatherForecast = async () => {
    setLoading(true)
    setError(null)
    
    try {
      const response = await fetch('/api/weatherforecast')
      
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`)
      }
      
      const data: WeatherForecast[] = await response.json()
      setWeatherData(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch weather data')
      console.error('Error fetching weather forecast:', err)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (activeTab === 'weather') {
        fetchWeatherForecast()
    }
  }, [activeTab])

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString(undefined, { 
      weekday: 'short', 
      month: 'short', 
      day: 'numeric' 
    })
  }

  return (
    <div className="app-container">
      <header className="app-header">
        <a 
          href="https://aspire.dev" 
          target="_blank" 
          rel="noopener noreferrer"
          aria-label="Visit Aspire website (opens in new tab)"
          className="logo-link"
        >
          <img src={aspireLogo} className="logo" alt="Aspire logo" />
        </a>
        <h1 className="app-title">EMMA Dashboard</h1>
        
        <nav className="main-nav">
             <button onClick={() => setActiveTab('dashboard')} className={activeTab === 'dashboard' ? 'active' : ''}>Energy Monitor</button>
             <button onClick={() => setActiveTab('map')} className={activeTab === 'map' ? 'active' : ''}>Map</button>
             <button onClick={() => setActiveTab('weather')} className={activeTab === 'weather' ? 'active' : ''}>Weather</button>
        </nav>
      </header>

      <main className="main-content">
        {activeTab === 'dashboard' ? (
            <Dashboard />
        ) : activeTab === 'map' ? (
            <MapDashboard />
        ) : (
        <section className="weather-section" aria-labelledby="weather-heading">
          {/* Weather Content */}
          <div className="card">
            <div className="section-header">
              <h2 id="weather-heading" className="section-title">Weather Forecast</h2>
              {/* ... Copied Weather Controls ... */}
              <div className="header-actions">
                 <button className="refresh-button" onClick={fetchWeatherForecast}>Refresh</button>
              </div>
            </div>
            
            {loading && <p>Loading...</p>}
            
            {weatherData.length > 0 && (
              <div className="weather-grid">
                {weatherData.map((forecast, index) => (
                  <article key={index} className="weather-card">
                    <h3 className="weather-date">{formatDate(forecast.date)}</h3>
                    <p className="weather-summary">{forecast.summary}</p>
                    <p>{useCelsius ? forecast.temperatureC : forecast.temperatureF}°{useCelsius ? 'C' : 'F'}</p>
                  </article>
                ))}
              </div>
            )}
          </div>
        </section>
        )}
      </main>
    </div>
  )
}

export default App
