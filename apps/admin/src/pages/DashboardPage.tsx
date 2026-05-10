import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { adminApi, type AnalyticsEvent, type AnalyticsSummary, type AuditEntry } from '../lib/api';
import { clearAdminAuth, loadAdminAuth, saveAdminAuth } from '../lib/auth';

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value));
}

function getAdminAuth() {
  const auth = loadAdminAuth();
  return auth?.user.role === 'Admin' ? auth : null;
}

export function DashboardPage() {
  const [auth, setAuth] = useState(() => getAdminAuth());
  const [summary, setSummary] = useState<AnalyticsSummary | null>(null);
  const [events, setEvents] = useState<AnalyticsEvent[]>([]);
  const [audit, setAudit] = useState<AuditEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  useEffect(() => {
    const syncAuth = () => setAuth(getAdminAuth());
    window.addEventListener('pass-it-on:admin-auth-changed', syncAuth);
    return () => window.removeEventListener('pass-it-on:admin-auth-changed', syncAuth);
  }, []);

  useEffect(() => {
    if (!auth) {
      return;
    }

    let cancelled = false;

    async function load() {
      try {
        setLoading(true);
        setMessage(null);
        const [summaryPayload, eventsPayload, auditPayload] = await Promise.all([
          adminApi.getSummary(),
          adminApi.getEvents(),
          adminApi.getAudit(),
        ]);
        if (!cancelled) {
          setSummary(summaryPayload);
          setEvents(eventsPayload.items);
          setAudit(auditPayload.items);
        }
      } catch (err) {
        if (!cancelled) {
          setMessage(err instanceof Error ? err.message : 'Unable to load dashboard data.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [auth]);

  async function signIn(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      setMessage('Signing in...');
      const nextAuth = await adminApi.login({ email, password });
      if (nextAuth.user.role !== 'Admin') {
        clearAdminAuth();
        setMessage('This account is authenticated but does not have the Admin role.');
        return;
      }

      saveAdminAuth(nextAuth);
      setMessage('Signed in.');
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to sign in.');
    }
  }

  async function signOut() {
    const current = loadAdminAuth();
    try {
      if (current) {
        await adminApi.logout(current.refreshToken);
      }
    } catch {
      // Local sign-out still matters.
    }

    clearAdminAuth();
    setSummary(null);
    setEvents([]);
    setAudit([]);
    setMessage('Signed out.');
  }

  if (!auth) {
    return (
      <section className="page-stack">
        <header className="section-heading">
          <p className="eyebrow">Overview</p>
          <h2>Admin sign-in</h2>
          <p className="section-copy">
            Use an account with the Admin role claim. Standard member accounts are rejected here.
          </p>
        </header>

        <form className="form-card narrow-card" onSubmit={signIn}>
          <label>
            <span>Email</span>
            <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
          </label>
          <label>
            <span>Password</span>
            <input type="password" value={password} onChange={(event) => setPassword(event.target.value)} required />
          </label>
          <button className="primary-button" type="submit">Sign in</button>
          {message ? <p className="status-banner">{message}</p> : null}
        </form>
      </section>
    );
  }

  const cards = summary
    ? [
        ['Active users', String(summary.totalActiveUsers)],
        ['Listings', String(summary.totalListings)],
        ['Open reports', String(summary.openReports)],
        ['Published bulletins', String(summary.publishedBulletins)],
      ]
    : [];

  return (
    <section className="page-stack">
      <header className="section-heading">
        <p className="eyebrow">Overview</p>
        <h2>Operations dashboard</h2>
        <p className="section-copy">Current snapshot of moderation and publishing activity.</p>
      </header>

      <div className="button-row">
        <button className="ghost-button" type="button" onClick={signOut}>Sign out</button>
        <Link className="primary-button" to="/reports">Open report queue</Link>
      </div>

      {loading ? <p className="status-banner">Loading admin dashboard...</p> : null}
      {message ? <p className="status-banner">{message}</p> : null}

      <div className="card-list metric-grid">
        {cards.map(([label, value]) => (
          <article key={label} className="info-card metric-card">
            <span className="info-label">{label}</span>
            <strong className="metric-value">{value}</strong>
          </article>
        ))}
      </div>

      <div className="dashboard-grid">
        <article className="info-card">
          <div className="section-heading compact-heading">
            <div>
              <p className="eyebrow">Recent analytics</p>
              <h3>Latest tracked events</h3>
            </div>
            <Link className="text-link" to="/analytics">View all</Link>
          </div>
          <div className="stack-list">
            {events.length > 0 ? events.map((event) => (
              <article key={event.id} className="stack-item">
                <strong>{event.eventName}</strong>
                <p>{event.subjectType}{event.subjectId ? ` · ${event.subjectId}` : ''}</p>
                <span className="card-meta">{formatDate(event.occurredAt)}</span>
              </article>
            )) : <p>No analytics events recorded yet.</p>}
          </div>
        </article>

        <article className="info-card">
          <div className="section-heading compact-heading">
            <div>
              <p className="eyebrow">Recent audit</p>
              <h3>Admin activity log</h3>
            </div>
            <Link className="text-link" to="/analytics">Audit trail</Link>
          </div>
          <div className="stack-list">
            {audit.length > 0 ? audit.map((entry) => (
              <article key={entry.id} className="stack-item">
                <strong>{entry.action}</strong>
                <p>{entry.targetType} · {entry.targetId}</p>
                <span className="card-meta">{formatDate(entry.createdAt)} · {entry.adminEmail}</span>
              </article>
            )) : <p>No audit entries yet.</p>}
          </div>
        </article>
      </div>
    </section>
  );
}
