import { useState, useEffect } from 'react'
import { Dashboard } from './pages/Dashboard';
import { MapDashboard } from './pages/MapDashboard';
import { Login } from './pages/Login';
import aspireLogo from '/Aspire.png'
import './App.css'

interface WeatherForecast {
  date: string
  temperatureC: number
  temperatureF: number
  summary: string
}

function App() {
  const [token, setToken] = useState<string | null>(localStorage.getItem('emma_token'));
  const [activeTab, setActiveTab] = useState<'weather' | 'dashboard' | 'map'>('dashboard');

  // Weather State
  const [weatherData, setWeatherData] = useState<WeatherForecast[]>([])
  const [loading, setLoading] = useState(false)
  const [useCelsius] = useState(false)

  const handleLogin = (newToken: string) => {
    setToken(newToken);
    localStorage.setItem('emma_token', newToken);
  };

  const handleLogout = () => {
    setToken(null);
    localStorage.removeItem('emma_token');
  };

  const fetchWeatherForecast = async () => {
    if (!token) return;
    setLoading(true)

    try {
      const response = await fetch('/api/weatherforecast', {
        headers: {
          'Authorization': `Bearer ${token}`
        }
      })

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`)
      }

      const data: WeatherForecast[] = await response.json()
      setWeatherData(data)
    } catch (err) {
      console.error('Error fetching weather forecast:', err)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (activeTab === 'weather' && token) {
      fetchWeatherForecast()
    }
  }, [activeTab, token])

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString(undefined, {
      weekday: 'short',
      month: 'short',
      day: 'numeric'
    })
  }

  if (!token) {
    return <Login onLogin={handleLogin} />;
  }

  return (
    <div className="app-container">
      <header className="app-header">
        <div className="header-left">
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
        </div>

        <nav className="main-nav">
          <button onClick={() => setActiveTab('dashboard')} className={activeTab === 'dashboard' ? 'active' : ''}>Energy Monitor</button>
          <button onClick={() => setActiveTab('map')} className={activeTab === 'map' ? 'active' : ''}>Map</button>
          <button onClick={() => setActiveTab('weather')} className={activeTab === 'weather' ? 'active' : ''}>Weather</button>
        </nav>

        <div className="header-right">
          <button className="logout-button" onClick={handleLogout}>Sign Out</button>
        </div>
      </header>

      <main className="main-content">
        {activeTab === 'dashboard' ? (
          <Dashboard token={token!} />
        ) : activeTab === 'map' ? (
          <MapDashboard token={token!} />
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
