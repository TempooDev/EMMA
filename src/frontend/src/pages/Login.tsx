import { useState } from 'react';

interface LoginProps {
  onLogin: (token: string) => void;
}

export function Login({ onLogin }: LoginProps) {
  const [username, setUsername] = useState('admin');
  const [password, setPassword] = useState('Admin123!');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const response = await fetch('/connect/token', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ username, password }),
      });

      if (response.ok) {
        const data = await response.json();
        onLogin(data.access_token);
      } else {
        const errorData = await response.json().catch(() => ({}));
        setError(errorData.error_description || 'Invalid credentials');
      }
    } catch (err) {
      setError('Connection failed. Please check if the identity service is running.');
      console.error('Login error:', err);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-container">
      <div className="login-card">
        <div className="login-header">
          <div className="login-logo">
            <span className="logo-accent">E</span>MMA
          </div>
          <h1>Welcome Back</h1>
          <p className="login-subtitle">Energy Market Monitoring & Analytics</p>
        </div>

        <form onSubmit={handleSubmit} className="login-form">
          <div className="input-group">
            <label htmlFor="username">Username</label>
            <input
              id="username"
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="Enter your username"
              required
            />
          </div>

          <div className="input-group">
            <label htmlFor="password">Password</label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              required
            />
          </div>

          {error && <div className="login-error">{error}</div>}

          <button type="submit" className="login-button" disabled={loading}>
            {loading ? <span className="loader"></span> : 'Sign In'}
          </button>
        </form>

        <div className="login-footer">
          <p>Demo credentials: <code>admin</code> / <code>Admin123!</code></p>
        </div>
      </div>

      <style>{`
        .login-container {
          display: flex;
          align-items: center;
          justify-content: center;
          min-height: 80vh;
          padding: 20px;
        }

        .login-card {
          background: rgba(30, 30, 30, 0.8);
          backdrop-filter: blur(10px);
          border: 1px solid rgba(255, 255, 255, 0.1);
          border-radius: 16px;
          padding: 40px;
          width: 100%;
          max-width: 400px;
          box-shadow: 0 20px 40px rgba(0, 0, 0, 0.4);
          animation: slideUp 0.6s cubic-bezier(0.16, 1, 0.3, 1);
        }

        @keyframes slideUp {
          from { opacity: 0; transform: translateY(20px); }
          to { opacity: 1; transform: translateY(0); }
        }

        .login-header {
          text-align: center;
          margin-bottom: 32px;
        }

        .login-logo {
          font-size: 2rem;
          font-weight: 800;
          letter-spacing: -1px;
          margin-bottom: 12px;
          color: #fff;
        }

        .logo-accent {
          color: #0078d4;
        }

        .login-header h1 {
          font-size: 1.5rem;
          margin: 0;
          color: #fff;
        }

        .login-subtitle {
          color: #888;
          font-size: 0.9rem;
          margin-top: 4px;
        }

        .login-form {
          display: flex;
          flex-direction: column;
          gap: 20px;
        }

        .input-group {
          display: flex;
          flex-direction: column;
          gap: 8px;
        }

        .input-group label {
          font-size: 0.85rem;
          font-weight: 500;
          color: #bbb;
        }

        .input-group input {
          background: #2a2a2a;
          border: 1px solid #444;
          border-radius: 8px;
          padding: 12px;
          color: #fff;
          font-size: 1rem;
          transition: border-color 0.2s, box-shadow 0.2s;
        }

        .input-group input:focus {
          outline: none;
          border-color: #0078d4;
          box-shadow: 0 0 0 3px rgba(0, 120, 212, 0.2);
        }

        .login-error {
          background: rgba(255, 69, 58, 0.1);
          color: #ff453a;
          padding: 12px;
          border-radius: 8px;
          font-size: 0.85rem;
          text-align: center;
          border: 1px solid rgba(255, 69, 58, 0.2);
        }

        .login-button {
          background: #0078d4;
          color: white;
          border: none;
          border-radius: 8px;
          padding: 14px;
          font-size: 1rem;
          font-weight: 600;
          cursor: pointer;
          transition: background 0.2s, transform 0.1s;
          display: flex;
          align-items: center;
          justify-content: center;
          margin-top: 8px;
        }

        .login-button:hover:not(:disabled) {
          background: #0086f0;
        }

        .login-button:active:not(:disabled) {
          transform: scale(0.98);
        }

        .login-button:disabled {
          opacity: 0.6;
          cursor: not-allowed;
        }

        .login-footer {
          margin-top: 32px;
          text-align: center;
          font-size: 0.8rem;
          color: #666;
        }

        .login-footer code {
          background: #2a2a2a;
          padding: 2px 4px;
          border-radius: 4px;
          color: #aaa;
        }

        .loader {
          width: 20px;
          height: 20px;
          border: 2px solid rgba(255, 255, 255, 0.3);
          border-radius: 50%;
          border-top-color: #fff;
          animation: spin 0.8s linear infinite;
        }

        @keyframes spin {
          to { transform: rotate(360deg); }
        }
      `}</style>
    </div>
  );
}
