import { useEffect, useState } from 'react';
import { NavLink, Route, Routes } from 'react-router-dom';
import { AnalyticsPage } from './pages/AnalyticsPage';
import { ContentPage } from './pages/ContentPage';
import { DashboardPage } from './pages/DashboardPage';
import { ListingsModerationPage } from './pages/ListingsModerationPage';
import { ReportsPage } from './pages/ReportsPage';
import { UsersPage } from './pages/UsersPage';
import { loadAdminAuth, type StoredAdminAuth } from './lib/auth';

const navigation = [
  { to: '/', label: 'Dashboard' },
  { to: '/users', label: 'Users' },
  { to: '/listings', label: 'Listings' },
  { to: '/reports', label: 'Reports' },
  { to: '/content', label: 'Content' },
  { to: '/analytics', label: 'Analytics' },
];

export default function App() {
  const [auth, setAuth] = useState<StoredAdminAuth | null>(() => loadAdminAuth());

  useEffect(() => {
    const syncAuth = () => setAuth(loadAdminAuth());
    window.addEventListener('pass-it-on:admin-auth-changed', syncAuth);
    return () => window.removeEventListener('pass-it-on:admin-auth-changed', syncAuth);
  }, []);

  return (
    <div className="app-shell admin-shell">
      <header className="app-header">
        <div className="brand-block">
          <p className="eyebrow">Pass It On</p>
          <h1>Admin Operations</h1>
          <p className="header-copy">
            Moderate users and listings, publish content, and review platform activity.
          </p>
        </div>
        <div className="header-actions">
          <nav className="main-nav" aria-label="Admin navigation">
            {navigation.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
                end={item.to === '/'}
              >
                {item.label}
              </NavLink>
            ))}
          </nav>
          <div className="session-pill">
            <span className="session-dot" aria-hidden="true" />
            {auth ? `${auth.user.displayName} · ${auth.user.role}` : 'Admin sign-in required'}
          </div>
        </div>
      </header>

      <main className="app-main">
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/users" element={<UsersPage />} />
          <Route path="/listings" element={<ListingsModerationPage />} />
          <Route path="/reports" element={<ReportsPage />} />
          <Route path="/content" element={<ContentPage />} />
          <Route path="/analytics" element={<AnalyticsPage />} />
        </Routes>
      </main>
    </div>
  );
}
