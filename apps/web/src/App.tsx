import { useEffect, useState } from 'react';
import { NavLink, Route, Routes } from 'react-router-dom';
import { AuthCallbackPage } from './pages/AuthCallbackPage';
import { CreateListingPage } from './pages/CreateListingPage';
import { FaqPage } from './pages/FaqPage';
import { HomePage } from './pages/HomePage';
import { ListingDetailPage } from './pages/ListingDetailPage';
import { ListingsPage } from './pages/ListingsPage';
import { PoliciesPage } from './pages/PoliciesPage';
import { ProfilePage } from './pages/ProfilePage';
import { loadAuth, type StoredAuth } from './lib/auth';

const navigation = [
  { to: '/', label: 'Home' },
  { to: '/listings', label: 'Listings' },
  { to: '/listings/new', label: 'Post Item' },
  { to: '/profile', label: 'Profile' },
  { to: '/faq', label: 'FAQ' },
  { to: '/policies', label: 'Policies' },
];

export default function App() {
  const [auth, setAuth] = useState<StoredAuth | null>(() => loadAuth());

  useEffect(() => {
    const syncAuth = () => setAuth(loadAuth());
    window.addEventListener('pass-it-on:auth-changed', syncAuth);
    return () => window.removeEventListener('pass-it-on:auth-changed', syncAuth);
  }, []);

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="brand-block">
          <p className="eyebrow">Pass It On</p>
          <h1>Community Exchange</h1>
          <p className="header-copy">
            Share children&apos;s clothing locally without resale, payments, or inbox overhead.
          </p>
        </div>
        <div className="header-actions">
          <nav className="main-nav" aria-label="Primary">
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
            {auth ? `Signed in as ${auth.user.displayName}` : 'Browsing as guest'}
          </div>
        </div>
      </header>

      <main className="app-main">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/auth/callback" element={<AuthCallbackPage />} />
          <Route path="/listings" element={<ListingsPage />} />
          <Route path="/listings/:listingId" element={<ListingDetailPage />} />
          <Route path="/listings/new" element={<CreateListingPage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/faq" element={<FaqPage />} />
          <Route path="/policies" element={<PoliciesPage />} />
        </Routes>
      </main>
    </div>
  );
}
